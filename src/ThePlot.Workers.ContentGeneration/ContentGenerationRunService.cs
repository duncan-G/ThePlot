using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Infrastructure;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationRunService(
    ThePlotContext db,
    VoiceDeterminationService voiceDetermination,
    GenerationGraphBuilder graphBuilder)
{
    public async Task<Guid> StartRunAsync(Guid screenplayId, CancellationToken ct)
    {
        var run = GenerationRun.Start(screenplayId);
        db.GenerationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }

    /// <summary>
    /// Phase 1: ensure voices / narrator; phase 2: build analysis + content graph and start run processing.
    /// </summary>
    public async Task CompleteVoiceDeterminationAndStartContentGenerationAsync(Guid runId, CancellationToken ct)
    {
        var run = await db.GenerationRuns.FirstAsync(r => r.Id == runId, ct);
        if (run.Phase != GenerationWorkflowPhase.CharacterResolution)
        {
            throw new InvalidOperationException("Run is not in voice-determination phase.");
        }

        await voiceDetermination.EnsureVoicesForScreenplayAsync(run.ScreenplayId, ct);
        run.AdvanceToContentGenerationPhase();
        run.MarkRunning();
        await db.SaveChangesAsync(ct);

        await graphBuilder.BuildGraphAsync(run, ct);

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
            .FirstAsync(n => n.Id == nodeId, ct);

        foreach (var artifact in (node.Artifacts ?? []).Where(a => a.IsCurrent))
        {
            artifact.Supersede();
        }

        node.ResetForRegeneration();
        await db.SaveChangesAsync(ct);
    }
}
