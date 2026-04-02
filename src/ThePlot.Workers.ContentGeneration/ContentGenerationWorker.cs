using Microsoft.Extensions.Options;
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var claimService = scope.ServiceProvider.GetRequiredService<GenerationNodeClaimService>();
                var executor = scope.ServiceProvider.GetRequiredService<GenerationNodeExecutor>();

                var work = await claimService.TryClaimNextAsync(_workerId, stoppingToken);
                if (work is null)
                {
                    await Task.Delay(options.Value.WorkerIdleDelay, stoppingToken);
                    continue;
                }

                await executor.ExecuteAsync(work, _workerId, stoppingToken);
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
}
