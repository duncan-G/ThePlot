using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OpenTelemetry;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ThePlot.Database.Abstractions;

namespace ThePlot.Workers.PdfValidation;

public class PdfValidationWorker(
    ILogger<PdfValidationWorker> logger,
    IHostEnvironment hostEnvironment,
    ServiceBusClient serviceBusClient,
    BlobServiceClient blobServiceClient,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    internal const string ActivitySourceName = "ThePlot.PdfValidation";
    internal const string ValidationQueue = "pdf-validation";
    internal const string UploadsContainer = "pdf-uploads";

    private const long MaxSizeBytes = 10L * 1024 * 1024; // 10 MB
    private const string PdfContentType = "application/pdf";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation("PdfValidationWorker starting in polling mode (development)");
            await RunPollingLoopAsync(stoppingToken);
        }
        else
        {
            logger.LogInformation("PdfValidationWorker starting in Service Bus mode (Event Grid). Queue: {Queue}", ValidationQueue);
            await RunServiceBusProcessorAsync(stoppingToken);
        }
    }

    // ── Production: Event Grid → Service Bus ──────────────────────────────

    private async Task RunServiceBusProcessorAsync(CancellationToken stoppingToken)
    {
        await using var processor = serviceBusClient.CreateProcessor(ValidationQueue, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        processor.ProcessMessageAsync += HandleServiceBusMessageAsync;
        processor.ProcessErrorAsync += HandleProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleServiceBusMessageAsync(ProcessMessageEventArgs args)
    {
        string? blobName = null;

        try
        {
            blobName = ExtractBlobNameFromCloudEvent(args.Message);

            if (blobName is null)
            {
                logger.LogWarning("Could not extract blob name from Event Grid message. Dead-lettering.");
                await args.DeadLetterMessageAsync(args.Message, "UnparseableEvent",
                    "Could not extract blob name from CloudEvent", args.CancellationToken);
                return;
            }

            await ValidateBlobAsync(blobName, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.AddException(ex);
            logger.LogError(ex, "Failed to validate blob {BlobName}", blobName ?? "unknown");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private static string? ExtractBlobNameFromCloudEvent(ServiceBusReceivedMessage message)
    {
        using var doc = JsonDocument.Parse(message.Body);
        var root = doc.RootElement;

        // CloudEvent schema: subject = "/blobServices/default/containers/pdf-uploads/blobs/{name}"
        if (root.TryGetProperty("subject", out var subject))
        {
            var subjectStr = subject.GetString();
            const string blobsPrefix = "/blobs/";
            var idx = subjectStr?.LastIndexOf(blobsPrefix, StringComparison.Ordinal);
            if (idx >= 0)
                return subjectStr![(idx.Value + blobsPrefix.Length)..];
        }

        return null;
    }

    private Task HandleProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, Namespace: {Namespace}, Entity: {Entity}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
        return Task.CompletedTask;
    }

    // ── Local dev: blob container polling ─────────────────────────────────

    private async Task RunPollingLoopAsync(CancellationToken stoppingToken)
    {
        var processedBlobs = new HashSet<string>(StringComparer.Ordinal);
        var container = blobServiceClient.GetBlobContainerClient(UploadsContainer);

        while (!stoppingToken.IsCancellationRequested)
        {
            var newBlobs = new List<string>();

            try
            {
                using (SuppressInstrumentationScope.Begin())
                {
                    await foreach (var blob in container.GetBlobsAsync(cancellationToken: stoppingToken))
                    {
                        if (processedBlobs.Add(blob.Name))
                            newBlobs.Add(blob.Name);
                    }
                }

                foreach (var blobName in newBlobs)
                {
                    logger.LogInformation("Poller detected new blob {BlobName}", blobName);

                    try
                    {
                        await ValidateBlobAsync(blobName, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Validation failed for blob {BlobName}", blobName);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Polling iteration failed. Retrying after delay.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    // ── Shared validation logic ───────────────────────────────────────────

    private async Task ValidateBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(UploadsContainer);
        var blobClient = container.GetBlobClient(blobName);

        Response<BlobProperties> props;
        ActivityContext parentContext;
        try
        {
            // Suppress instrumentation to avoid an activity being created without a parent.
            using (SuppressInstrumentationScope.Begin())
            {
                props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            }

            parentContext = GetParentActivityContext(props.Value.Metadata);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to retrieve blob properties for {BlobName}. Sending ImportFailed status.", blobName);
            try
            {
                await SendStatusAsync(
                    ScreenplayImportStatusMessage.ImportFailed(null, blobName, $"Import error: {ex.Message}"),
                    cancellationToken);
            }
            catch (Exception statusEx)
            {
                logger.LogError(statusEx, "Failed to send ImportFailed status for blob {BlobName}", blobName);
            }

            throw;
        }

        using var activity = ActivitySource.StartActivity(
            "Validating PDF",
            ActivityKind.Consumer,
            parentContext: parentContext);

        activity?.SetTag("blob.name", blobName);
        logger.LogInformation("Validating uploaded blob {BlobName}", blobName);

        try
        {
            await SendStatusAsync(ScreenplayImportStatusMessage.BlobUploaded(blobName), cancellationToken);

            var size = props.Value.ContentLength;
            var contentType = props.Value.ContentType ?? string.Empty;

            if (size > MaxSizeBytes)
            {
                await RejectAndDeleteAsync(blobClient, blobName, activity,
                    "size_exceeded", $"exceeds 10 MB limit ({size} bytes)", cancellationToken);
                return;
            }

            if (!contentType.Equals(PdfContentType, StringComparison.OrdinalIgnoreCase))
            {
                await RejectAndDeleteAsync(blobClient, blobName, activity,
                    "invalid_content_type", $"has invalid content type '{contentType}'", cancellationToken);
                return;
            }

            if (!await HasValidPdfSignatureAsync(blobClient, cancellationToken))
            {
                await RejectAndDeleteAsync(blobClient, blobName, activity,
                    "invalid_signature", "does not have valid PDF signature", cancellationToken);
                return;
            }

            activity?.SetTag("validation.result", "passed");
            activity?.SetTag("blob.size", size);
            logger.LogInformation("Blob {BlobName} passed validation ({Size} bytes, PDF).", blobName, size);

            await EnqueueForSplittingAsync(blobName, size, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            logger.LogError(ex, "Unhandled error validating blob {BlobName}. Sending ValidationFailed status.", blobName);
            try
            {
                await SendStatusAsync(
                    ScreenplayImportStatusMessage.ValidationFailed(blobName, $"Validation error: {ex.Message}"),
                    cancellationToken);
            }
            catch (Exception statusEx)
            {
                logger.LogError(statusEx, "Failed to send ValidationFailed status for blob {BlobName}", blobName);
            }

            throw;
        }
    }

    private async Task EnqueueForSplittingAsync(string blobName, long blobSize, CancellationToken cancellationToken)
    {
        var placeholderTitle = Path.GetFileNameWithoutExtension(blobName);
        if (string.IsNullOrWhiteSpace(placeholderTitle))
            placeholderTitle = "Untitled";

        await using var scope = scopeFactory.CreateAsyncScope();
        var screenplayRepository = scope.ServiceProvider.GetRequiredService<IScreenplayRepository>();
        var unitOfWorkFactory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        using var uow = unitOfWorkFactory.CreateReadWrite("CreateScreenplayPlaceholder");
        var screenplay = Screenplay.Create(placeholderTitle);
        var screenplayId = screenplay.Id;
        await screenplayRepository.AddAsync(screenplay, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        await uow.CommitAsync(cancellationToken);

        await SendStatusAsync(ScreenplayImportStatusMessage.ValidationPassed(screenplayId, blobName), cancellationToken);

        await using var sender = serviceBusClient.CreateSender("pdf-splitting-priority");

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(new
        {
            BlobName = blobName,
            BlobSize = blobSize,
            StartPage = 1,
            EndPage = 10,
            ScreenplayId = screenplayId,
            EnqueuedAt = DateTimeOffset.UtcNow
        }, JsonOptions))
        {
            ContentType = "application/json"
        };

        if (FormatTraceParent(Activity.Current) is { } traceparent)
        {
            message.ApplicationProperties["traceparent"] = traceparent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
        logger.LogInformation(
            "Created screenplay {ScreenplayId} and enqueued blob {BlobName} for priority splitting (pages 1-10).",
            screenplayId, blobName);
    }

    private async Task RejectAndDeleteAsync(
        BlobClient blobClient,
        string blobName,
        Activity? activity,
        string reasonTag,
        string logReason,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Blob {BlobName} {LogReason}. Sending ValidationFailed and deleting blob.", blobName, logReason);

        activity?.SetTag("validation.result", "rejected");
        activity?.SetTag("validation.reason", reasonTag);

        await SendStatusAsync(ScreenplayImportStatusMessage.ValidationFailed(blobName, logReason), cancellationToken);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task SendStatusAsync(ScreenplayImportStatusMessage message, CancellationToken cancellationToken)
    {
        await using var sender = serviceBusClient.CreateSender("screenplay-import-status");
        var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message, JsonOptions))
        {
            ContentType = "application/json"
        };

        if (FormatTraceParent(Activity.Current) is { } traceparent)
        {
            sbMessage.ApplicationProperties["traceparent"] = traceparent;
        }

        await sender.SendMessageAsync(sbMessage, cancellationToken);
    }

    private static async Task<bool> HasValidPdfSignatureAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        var header = new byte[PdfMagicBytes.Length];
        var readBytes = await stream.ReadAsync(header.AsMemory(), cancellationToken);

        return readBytes >= PdfMagicBytes.Length &&
               header.AsSpan(0, PdfMagicBytes.Length).SequenceEqual(PdfMagicBytes);
    }

    private static string? FormatTraceParent(Activity? activity)
    {
        if (activity is null || activity.TraceId == default || activity.SpanId == default)
            return null;

        return $"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.ActivityTraceFlags:x2}";
    }

    private static ActivityContext GetParentActivityContext(IDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("traceparent", out var traceparent) &&
            ActivityContext.TryParse(traceparent, null, out var parsedContext))
        {
            return parsedContext;
        }

        return default;
    }
}
