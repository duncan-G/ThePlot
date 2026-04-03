using System.Diagnostics;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationRunService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IGenerationRunRepository runRepository,
    IGenerationNodeRepository nodeRepository,
    IQueryFactory<GenerationRun, IGenerationRunQuery> runQueryFactory,
    IQueryFactory<GenerationNode, IGenerationNodeQuery> nodeQueryFactory)
{
    public async Task<Guid> StartRunAsync(Guid screenplayId, bool cancelActive, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("StartRun");
        using var activity = ContentGenerationTelemetry.StartActivity("ContentGeneration.Run");
        activity?.SetTag("contentgen.screenplay_id", screenplayId.ToString());

        var activeRunQuery = runQueryFactory.Create()
            .ByScreenplayId(screenplayId)
            .ByStatuses([GenerationRunStatus.Pending, GenerationRunStatus.Running]);
        var activeRun = await runRepository.GetFirstByQueryAsync(activeRunQuery, ct);

        if (activeRun is not null)
        {
            if (!cancelActive)
            {
                activity?.SetTag("contentgen.blocked_by_active_run", activeRun.Id.ToString());
                throw new GenerationAlreadyActiveException(activeRun.Id, screenplayId);
            }

            await CancelRunAsync(uow, activeRun, ct);
            activity?.SetTag("contentgen.cancelled_run_id", activeRun.Id.ToString());
        }

        var traceParent = ContentGenerationTelemetry.FormatTraceParent(activity);
        var run = GenerationRun.Start(screenplayId, traceParent);
        await runRepository.AddAsync(run, ct);
        await uow.CommitAsync(ct);

        activity?.SetTag("contentgen.run_id", run.Id.ToString());
        return run.Id;
    }

    private async Task CancelRunAsync(IUnitOfWork uow, GenerationRun run, CancellationToken ct)
    {
        run.MarkCancelled("Superseded by a new generation run.");

        var allNodes = await nodeRepository.GetByQueryAsync(
            nodeQueryFactory.Create().ByRunId(run.Id), ct);

        foreach (var node in allNodes.Where(n =>
            n.Status != GenerationNodeStatus.Succeeded
            && n.Status != GenerationNodeStatus.Failed
            && n.Status != GenerationNodeStatus.Blocked
            && n.Status != GenerationNodeStatus.Cancelled))
        {
            node.MarkCancelled();
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Transitions the run from <see cref="GenerationWorkflowPhase.CharacterResolution"/>
    /// to <see cref="GenerationWorkflowPhase.VoiceDetermination"/> so the background worker
    /// picks it up for voice assignment and graph construction.
    /// </summary>
    /// <returns>The run's stored trace-parent for distributed tracing continuity.</returns>
    public async Task<string?> EnqueueVoiceDeterminationAsync(Guid runId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("EnqueueVoiceDetermination");

        var run = await runRepository.GetByKeyAsync(runId, ct)
            ?? throw new InvalidOperationException($"Generation run {runId} not found.");

        using var activity = ContentGenerationTelemetry.StartActivity(
            "ContentGeneration.EnqueueVoiceDetermination", run.TraceParent);
        activity?.SetTag("contentgen.run_id", runId.ToString());

        if (run.Phase != GenerationWorkflowPhase.CharacterResolution)
        {
            throw new InvalidOperationException("Run is not in character-resolution phase.");
        }

        run.AdvanceToVoiceDeterminationPhase();
        run.MarkRunning();
        await uow.CommitAsync(ct);

        return run.TraceParent;
    }

    /// <summary>
    /// Executes voice determination, builds the generation graph, and advances the run to
    /// <see cref="GenerationWorkflowPhase.ContentGeneration"/>. Called by the background worker
    /// under a parent activity restored from the run's stored trace context.
    /// </summary>
    public async Task ProcessVoiceDeterminationAsync(
        Guid runId,
        VoiceDeterminationService voiceDetermination,
        GenerationGraphBuilder graphBuilder,
        CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("ProcessVoiceDetermination");

        var run = await runRepository.GetByKeyAsync(runId, ct)
            ?? throw new InvalidOperationException($"Generation run {runId} not found.");

        if (run.Phase != GenerationWorkflowPhase.VoiceDetermination)
        {
            throw new InvalidOperationException("Run is not in voice-determination phase.");
        }

        using (var voiceSpan = ContentGenerationTelemetry.StartActivity("ContentGeneration.EnsureVoices"))
        {
            voiceSpan?.SetTag("contentgen.screenplay_id", run.ScreenplayId.ToString());
            await voiceDetermination.EnsureVoicesForScreenplayAsync(run.ScreenplayId, ct);
        }

        run.AdvanceToContentGenerationPhase();
        await uow.SaveChangesAsync(ct);

        using (var graphSpan = ContentGenerationTelemetry.StartActivity("ContentGeneration.BuildGraph"))
        {
            graphSpan?.SetTag("contentgen.run_id", runId.ToString());
            await graphBuilder.BuildGraphAsync(run, ct);
        }

        var analysisQuery = nodeQueryFactory.Create()
            .ByRunId(runId)
            .ByKind(GenerationNodeKind.PreGenerationAnalysis)
            .ByStatus(GenerationNodeStatus.Blocked);
        var analysisBlocked = await nodeRepository.ExistsByQueryAsync(analysisQuery, ct);

        if (analysisBlocked)
        {
            var analysisNodeQuery = nodeQueryFactory.Create()
                .ByRunId(runId)
                .ByKind(GenerationNodeKind.PreGenerationAnalysis);
            var analysisNode = await nodeRepository.GetFirstByQueryAsync(analysisNodeQuery, ct)
                ?? throw new InvalidOperationException("Pre-generation analysis node not found.");

            Activity.Current?.SetTag("contentgen.analysis_blocked", true);
            run.MarkFailed(analysisNode.LastErrorMessage ?? "Pre-generation analysis blocked the run.");
        }

        await uow.CommitAsync(ct);
    }

    public async Task ReplayRunAsync(Guid runId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("ReplayRun");

        var nodeQuery = nodeQueryFactory.Create()
            .ByRunId(runId)
            .ByNotStatus(GenerationNodeStatus.Succeeded);
        var nodes = await nodeRepository.GetByQueryAsync(nodeQuery, ct);

        foreach (var node in nodes)
        {
            if (node.Status == GenerationNodeStatus.Running)
            {
                node.ClearStaleLeaseIfExpired(DateTime.UtcNow);
            }

            node.ReleaseLease();
            if (node.Status is GenerationNodeStatus.Blocked or GenerationNodeStatus.Cancelled)
            {
                continue;
            }

            node.MarkReady();
        }

        var run = await runRepository.GetByKeyAsync(runId, ct)
            ?? throw new InvalidOperationException($"Generation run {runId} not found.");
        run.MarkRunning();
        await uow.CommitAsync(ct);
    }

    public async Task RegenerateNodeAsync(Guid nodeId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("RegenerateNode");

        var nodeQuery = nodeQueryFactory.Create()
            .ById(nodeId)
            .IncludeArtifacts()
            .IncludeRun();
        var node = await nodeRepository.GetFirstByQueryAsync(nodeQuery, ct)
            ?? throw new InvalidOperationException($"Generation node {nodeId} not found.");

        foreach (var artifact in (node.Artifacts ?? []).Where(a => a.IsCurrent))
        {
            artifact.Supersede();
        }

        node.ResetForRegeneration();

        var run = node.GenerationRun!;
        if (run.Status is GenerationRunStatus.Completed or GenerationRunStatus.Failed)
        {
            run.MarkRunning();
        }

        await uow.CommitAsync(ct);
    }
}
