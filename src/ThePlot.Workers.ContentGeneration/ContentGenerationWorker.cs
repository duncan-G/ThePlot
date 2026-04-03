using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;
using ThePlot.Infrastructure.ContentGeneration;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationWorker(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ContentGenerationOptions> options,
    ILogger<ContentGenerationWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.CreateVersion7():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Content generation worker {WorkerId} started.", _workerId);

        await using var processor = serviceBusClient.CreateProcessor(
            ContentGenerationWorkPublisher.QueueName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false,
            });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        ContentGenerationWorkMessage? message;
        try
        {
            message = args.Message.Body.ToObjectFromJson<ContentGenerationWorkMessage>(
                ContentGenerationWorkMessage.JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize content generation work message. Dead-lettering.");
            await args.DeadLetterMessageAsync(
                args.Message, "DeserializationFailed", ex.Message, args.CancellationToken);
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message, "NullMessage", "Message body was null.", args.CancellationToken);
            return;
        }

        try
        {
            switch (message.Kind)
            {
                case ContentGenerationWorkMessage.VoiceDetermination:
                    if (message.RunId is not { } runId)
                    {
                        logger.LogWarning("VoiceDetermination message missing RunId. Dead-lettering.");
                        await args.DeadLetterMessageAsync(
                            args.Message, "MissingRunId", "RunId is required.", args.CancellationToken);
                        return;
                    }

                    await ProcessVoiceDeterminationAsync(runId, message.TraceParent, args.CancellationToken);
                    break;

                case ContentGenerationWorkMessage.TtsWorkAvailable:
                    await ProcessTtsBatchAsync(args.CancellationToken);
                    break;

                default:
                    logger.LogWarning("Unknown message kind: {Kind}. Dead-lettering.", message.Kind);
                    await args.DeadLetterMessageAsync(
                        args.Message, "UnknownKind", $"Unknown kind: {message.Kind}", args.CancellationToken);
                    return;
            }

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process content generation work message (Kind={Kind}).", message.Kind);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private async Task ProcessVoiceDeterminationAsync(Guid runId, string? traceParent, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var runRepository = sp.GetRequiredService<IGenerationRunRepository>();
        var unitOfWorkFactory = sp.GetRequiredService<IUnitOfWorkFactory>();
        var runService = sp.GetRequiredService<ContentGenerationRunService>();
        var voiceDet = sp.GetRequiredService<VoiceDeterminationService>();
        var graphBuilder = sp.GetRequiredService<GenerationGraphBuilder>();
        var publisher = sp.GetRequiredService<ContentGenerationWorkPublisher>();

        using var activity = ContentGenerationTelemetry.StartActivity(
            "ContentGeneration.VoiceDetermination", traceParent);
        activity?.SetTag("contentgen.run_id", runId.ToString());
        activity?.SetTag("contentgen.worker_id", _workerId);

        try
        {
            await runService.ProcessVoiceDeterminationAsync(runId, voiceDet, graphBuilder, ct);
            logger.LogInformation("Voice determination completed for run {RunId}. Publishing TTS work.", runId);
            await publisher.PublishTtsWorkAvailableAsync(runId, traceParent, ct);
        }
        catch (Exception ex)
        {
            ContentGenerationTelemetry.RecordError(activity, ex);
            logger.LogError(ex, "Voice determination failed for run {RunId}.", runId);

            using var writeUow = unitOfWorkFactory.CreateReadWrite("MarkRunFailed");
            var runEntity = await runRepository.GetByKeyAsync(runId, ct);
            if (runEntity is not null)
            {
                runEntity.MarkFailed($"Voice determination failed: {ex.Message}");
                await writeUow.CommitAsync(ct);
            }
        }
    }

    private async Task ProcessTtsBatchAsync(CancellationToken ct)
    {
        var batchSize = options.Value.TtsBatchSize;
        var claimed = new List<ClaimedGenerationWork>(batchSize);

        for (var i = 0; i < batchSize; i++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var claimService = scope.ServiceProvider.GetRequiredService<IGenerationNodeClaimService>();
            var work = await claimService.TryClaimNextAsync(_workerId, ct);
            if (work is null)
                break;
            claimed.Add(work);
        }

        if (claimed.Count == 0)
            return;

        logger.LogInformation("Claimed {Count} TTS node(s), executing concurrently.", claimed.Count);

        await Task.WhenAll(claimed.Select(work => ExecuteTtsInScopeAsync(work, ct)));

        // Re-enqueue so the next batch of work (unblocked dependencies, other runs) is picked up.
        await using var publishScope = scopeFactory.CreateAsyncScope();
        var publisher = publishScope.ServiceProvider.GetRequiredService<ContentGenerationWorkPublisher>();
        await publisher.PublishTtsWorkAvailableAsync(null, null, ct);
    }

    private async Task ExecuteTtsInScopeAsync(ClaimedGenerationWork work, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<GenerationNodeExecutor>();
        await executor.ExecuteAsync(work, _workerId, ct);
    }

    private Task HandleProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, Entity: {Entity}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
