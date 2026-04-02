using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Infrastructure;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationRunService(ThePlotContext db)
{
    public async Task<Guid> StartRunAsync(Guid screenplayId, bool cancelActive, CancellationToken ct)
    {
        using var activity = ContentGenerationTelemetry.StartActivity("ContentGeneration.Run");
        activity?.SetTag("contentgen.screenplay_id", screenplayId.ToString());

        var activeRun = await db.GenerationRuns
            .FirstOrDefaultAsync(r => r.ScreenplayId == screenplayId
                && (r.Status == GenerationRunStatus.Pending || r.Status == GenerationRunStatus.Running), ct);

        if (activeRun is not null)
        {
            if (!cancelActive)
            {
                activity?.SetTag("contentgen.blocked_by_active_run", activeRun.Id.ToString());
                throw new GenerationAlreadyActiveException(activeRun.Id, screenplayId);
            }

            await CancelRunAsync(activeRun, ct);
            activity?.SetTag("contentgen.cancelled_run_id", activeRun.Id.ToString());
        }

        var traceParent = ContentGenerationTelemetry.FormatTraceParent(activity);
        var run = GenerationRun.Start(screenplayId, traceParent);
        db.GenerationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        activity?.SetTag("contentgen.run_id", run.Id.ToString());
        return run.Id;
    }

    private async Task CancelRunAsync(GenerationRun run, CancellationToken ct)
    {
        run.MarkCancelled("Superseded by a new generation run.");

        var activeNodes = await db.GenerationNodes
            .Where(n => n.GenerationRunId == run.Id
                && n.Status != GenerationNodeStatus.Succeeded
                && n.Status != GenerationNodeStatus.Failed
                && n.Status != GenerationNodeStatus.Blocked
                && n.Status != GenerationNodeStatus.Cancelled)
            .ToListAsync(ct);

        foreach (var node in activeNodes)
        {
            node.MarkCancelled();
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Transitions the run from <see cref="GenerationWorkflowPhase.CharacterResolution"/>
    /// to <see cref="GenerationWorkflowPhase.VoiceDetermination"/> so the background worker
    /// picks it up for voice assignment and graph construction.
    /// </summary>
    public async Task EnqueueVoiceDeterminationAsync(Guid runId, CancellationToken ct)
    {
        var run = await db.GenerationRuns.FirstAsync(r => r.Id == runId, ct);

        using var activity = ContentGenerationTelemetry.StartActivity(
            "ContentGeneration.EnqueueVoiceDetermination", run.TraceParent);
        activity?.SetTag("contentgen.run_id", runId.ToString());

        if (run.Phase != GenerationWorkflowPhase.CharacterResolution)
        {
            throw new InvalidOperationException("Run is not in character-resolution phase.");
        }

        run.AdvanceToVoiceDeterminationPhase();
        run.MarkRunning();
        await db.SaveChangesAsync(ct);
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
        var run = await db.GenerationRuns.FirstAsync(r => r.Id == runId, ct);
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
        await db.SaveChangesAsync(ct);

        using (var graphSpan = ContentGenerationTelemetry.StartActivity("ContentGeneration.BuildGraph"))
        {
            graphSpan?.SetTag("contentgen.run_id", runId.ToString());
            await graphBuilder.BuildGraphAsync(run, ct);
        }

        var analysisBlocked = await db.GenerationNodes.AnyAsync(
            n => n.GenerationRunId == runId
                && n.Kind == GenerationNodeKind.PreGenerationAnalysis
                && n.Status == GenerationNodeStatus.Blocked,
            ct);

        if (analysisBlocked)
        {
            var analysisNode = await db.GenerationNodes.FirstAsync(
                n => n.GenerationRunId == runId && n.Kind == GenerationNodeKind.PreGenerationAnalysis,
                ct);

            Activity.Current?.SetTag("contentgen.analysis_blocked", true);
            run.MarkFailed(analysisNode.LastErrorMessage ?? "Pre-generation analysis blocked the run.");
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ReplayRunAsync(Guid runId, CancellationToken ct)
    {
        var nodes = await db.GenerationNodes
            .Where(n => n.GenerationRunId == runId && n.Status != GenerationNodeStatus.Succeeded)
            .ToListAsync(ct);

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

        var run = await db.GenerationRuns.FirstAsync(r => r.Id == runId, ct);
        run.MarkRunning();
        await db.SaveChangesAsync(ct);
    }

    public async Task RegenerateNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await db.GenerationNodes
            .Include(n => n.Artifacts)
            .Include(n => n.GenerationRun)
            .FirstAsync(n => n.Id == nodeId, ct);

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

        await db.SaveChangesAsync(ct);
    }
}
