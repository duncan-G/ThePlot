using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ThePlot.Core.ContentGeneration;
using ThePlot.Infrastructure;
using ThePlot.Infrastructure.ContentGeneration;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentGenerationOptions> options,
    ILogger<ContentGenerationWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.CreateVersion7():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Content generation worker {WorkerId} started.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var didWork = await RunOneCycleAsync(stoppingToken);
                if (!didWork)
                {
                    await Task.Delay(options.Value.WorkerIdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Content generation worker loop error.");
                await Task.Delay(options.Value.WorkerIdleDelay, stoppingToken);
            }
        }
    }

    /// <returns><c>true</c> if any work was performed; <c>false</c> to trigger an idle delay.</returns>
    private async Task<bool> RunOneCycleAsync(CancellationToken ct)
    {
        // Phase 1 — Voice determination (chat + embedding models)
        if (await TryProcessVoiceDeterminationBatchAsync(ct))
            return true;

        // Phase 2 — TTS content generation
        if (await TryProcessTtsBatchAsync(ct))
            return true;

        return false;
    }

    private async Task<bool> TryProcessVoiceDeterminationBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ThePlotContext>();

        var runs = await db.GenerationRuns
            .Where(r => r.Phase == GenerationWorkflowPhase.VoiceDetermination
                        && r.Status == GenerationRunStatus.Running)
            .OrderBy(r => r.DateCreated)
            .Select(r => new { r.Id, r.TraceParent })
            .ToListAsync(ct);

        if (runs.Count == 0)
            return false;

        logger.LogInformation("Found {Count} run(s) awaiting voice determination.", runs.Count);

        var runService = scope.ServiceProvider.GetRequiredService<ContentGenerationRunService>();
        var voiceDet = scope.ServiceProvider.GetRequiredService<VoiceDeterminationService>();
        var graphBuilder = scope.ServiceProvider.GetRequiredService<GenerationGraphBuilder>();

        foreach (var run in runs)
        {
            using var activity = ContentGenerationTelemetry.StartActivity(
                "ContentGeneration.VoiceDetermination", run.TraceParent);
            activity?.SetTag("contentgen.run_id", run.Id.ToString());
            activity?.SetTag("contentgen.worker_id", _workerId);

            try
            {
                await runService.ProcessVoiceDeterminationAsync(run.Id, voiceDet, graphBuilder, ct);
                logger.LogInformation("Voice determination completed for run {RunId}.", run.Id);
            }
            catch (Exception ex)
            {
                ContentGenerationTelemetry.RecordError(activity, ex);
                logger.LogError(ex, "Voice determination failed for run {RunId}.", run.Id);
                var runEntity = await db.GenerationRuns.FirstAsync(r => r.Id == run.Id, ct);
                runEntity.MarkFailed($"Voice determination failed: {ex.Message}");
                await db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    private async Task<bool> TryProcessTtsBatchAsync(CancellationToken ct)
    {
        var batchSize = options.Value.TtsBatchSize;
        var claimed = new List<ClaimedGenerationWork>(batchSize);

        for (var i = 0; i < batchSize; i++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var claimService = scope.ServiceProvider.GetRequiredService<GenerationNodeClaimService>();
            var work = await claimService.TryClaimNextAsync(_workerId, ct);
            if (work is null)
                break;
            claimed.Add(work);
        }

        if (claimed.Count == 0)
            return false;

        logger.LogInformation("Claimed {Count} TTS node(s), executing concurrently.", claimed.Count);

        await Task.WhenAll(claimed.Select(work => ExecuteTtsInScopeAsync(work, ct)));
        return true;
    }

    private async Task ExecuteTtsInScopeAsync(ClaimedGenerationWork work, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<GenerationNodeExecutor>();
        await executor.ExecuteAsync(work, _workerId, ct);
    }
}
