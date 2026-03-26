using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Workers.PdfProcessing.Parsing;

namespace ThePlot.Workers.PdfProcessing;

public class PdfProcessingWorker(
    ILogger<PdfProcessingWorker> logger,
    ServiceBusClient serviceBusClient,
    BlobServiceClient blobServiceClient,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    internal const string ActivitySourceName = "ThePlot.PdfProcessing";
    public const string PriorityQueue = "pdf-processing-priority";
    public const string StandardQueue = "pdf-processing-standard";

    private const string ChunksContainer = "pdf-chunks";
    private const int DefaultChunkTimeoutSeconds = 5;

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "PdfProcessingWorker starting. Priority: {PriorityQueue}, Standard: {StandardQueue}",
            PriorityQueue, StandardQueue);

        await using var priorityProcessor = serviceBusClient.CreateProcessor(PriorityQueue, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });
        await using var standardProcessor = serviceBusClient.CreateProcessor(StandardQueue, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        priorityProcessor.ProcessMessageAsync += args => HandleMessageAsync(args, isPriority: true);
        priorityProcessor.ProcessErrorAsync += HandleProcessErrorAsync;

        standardProcessor.ProcessMessageAsync += args => HandleMessageAsync(args, isPriority: false);
        standardProcessor.ProcessErrorAsync += HandleProcessErrorAsync;

        await priorityProcessor.StartProcessingAsync(stoppingToken);
        await standardProcessor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        await priorityProcessor.StopProcessingAsync(CancellationToken.None);
        await standardProcessor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args, bool isPriority)
    {
        ProcessRequest? request = null;

        try
        {
            request = DeserializeRequest(args.Message);
            var traceparent = GetTraceparent(args.Message);
            using var activity = StartTelemetryActivity(request, isPriority, traceparent);

            logger.LogInformation(
                "Processing {Queue} chunk: blob={ChunkBlob}, pages {Start}-{End}, screenplay={ScreenplayId}",
                isPriority ? "priority" : "standard", request.ChunkBlobName, request.StartPage, request.EndPage, request.ScreenplayId);

            var timeoutSeconds = configuration.GetValue("PdfProcessing:ChunkTimeoutSeconds", DefaultChunkTimeoutSeconds);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await ExecuteProcessingWorkflowAsync(request, activity, timeoutCts.Token);
            await TryDeleteChunkBlobAsync(request.ChunkBlobName, args.CancellationToken);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (OperationCanceledException ex) when (args.CancellationToken.IsCancellationRequested == false)
        {
            RecordTelemetryError(ex);
            logger.LogWarning(ex, "Chunk processing timed out for {ChunkBlob}", request?.ChunkBlobName ?? "unknown");
            await TryDeleteChunkBlobAsync(request?.ChunkBlobName, args.CancellationToken);
            await SetChunkProcessFailedAndDeadLetterAsync(request, "Chunk processing timed out", args);
        }
        catch (Exception ex)
        {
            RecordTelemetryError(ex);
            logger.LogError(ex, "Failed to process chunk {ChunkBlob}", request?.ChunkBlobName ?? "unknown");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private async Task SetChunkProcessFailedAndDeadLetterAsync(ProcessRequest? request, string message, ProcessMessageEventArgs args)
    {
        if (request is not null)
        {
            try
            {
                await SendStatusAsync(ScreenplayImportStatusMessage.ChunkProcessFailed(request.ScreenplayId, request.StartPage, message), args.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send ChunkProcessFailed status before dead-lettering");
            }
        }

        await args.DeadLetterMessageAsync(args.Message, "PdfProcessFailed", message, args.CancellationToken);
    }

    private async Task TryDeleteChunkBlobAsync(string? chunkBlobName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chunkBlobName)) return;
        try
        {
            var container = blobServiceClient.GetBlobContainerClient(ChunksContainer);
            await container.GetBlobClient(chunkBlobName).DeleteIfExistsAsync(cancellationToken: ct);
            logger.LogInformation("Deleted chunk blob {ChunkBlob}", chunkBlobName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete chunk blob {ChunkBlob}. Lifecycle policy will clean it up.", chunkBlobName);
        }
    }

    private Task HandleProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, Namespace: {Namespace}, Entity: {Entity}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
        return Task.CompletedTask;
    }

    private async Task ExecuteProcessingWorkflowAsync(
        ProcessRequest request,
        Activity? activity,
        CancellationToken ct)
    {
        var container = blobServiceClient.GetBlobContainerClient(ChunksContainer);
        var blobClient = container.GetBlobClient(request.ChunkBlobName);

        using var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms, ct);
        var pdfBytes = ms.ToArray();

        activity?.SetTag("pdf.chunk_size_bytes", pdfBytes.Length);

        ParsedScreenplay parsed;
        try
        {
            parsed = ScreenplayParser.Parse(pdfBytes, request.StartPage);
        }
        catch (ScreenplayParseException ex)
        {
            logger.LogWarning(ex,
                "Could not parse screenplay chunk {ChunkBlob} (pages {Start}-{End})",
                request.ChunkBlobName, request.StartPage, request.EndPage);
            activity?.SetTag("pdf.result", "parse_failed");
            activity?.SetTag("pdf.parse_error", ex.Message);
            await SendStatusAsync(ScreenplayImportStatusMessage.ChunkProcessFailed(request.ScreenplayId, request.StartPage, ex.Message), ct);
            return;
        }

        activity?.SetTag("pdf.scene_count", parsed.Scenes.Count);

        logger.LogInformation(
            "Parsed chunk {ChunkBlob}: {SceneCount} scenes from pages {Start}-{End}",
            request.ChunkBlobName, parsed.Scenes.Count, request.StartPage, request.EndPage);

        await using var scope = scopeFactory.CreateAsyncScope();
        var persistence = scope.ServiceProvider.GetRequiredService<ScreenplayPersistenceService>();
        await persistence.SaveChunkAsync(request.ScreenplayId, request.SourceBlobName, request.StartPage, parsed, ct);

        await SendStatusAsync(ScreenplayImportStatusMessage.ChunkProcessDone(request.ScreenplayId, request.StartPage), ct);

        activity?.SetTag("pdf.result", "completed");
        logger.LogInformation("Completed processing chunk {ChunkBlob}", request.ChunkBlobName);
    }

    private async Task SendStatusAsync(ScreenplayImportStatusMessage message, CancellationToken ct)
    {
        await using var sender = serviceBusClient.CreateSender("screenplay-import-status");
        var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message, JsonOptions))
        {
            ContentType = "application/json"
        };
        if (FormatTraceParent(Activity.Current) is { } traceparent)
            sbMessage.ApplicationProperties["traceparent"] = traceparent;
        await sender.SendMessageAsync(sbMessage, ct);
    }

    private static ProcessRequest DeserializeRequest(ServiceBusReceivedMessage message)
    {
        return message.Body.ToObjectFromJson<ProcessRequest>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize process request");
    }

    private static string? GetTraceparent(ServiceBusReceivedMessage message)
    {
        return message.ApplicationProperties.TryGetValue("traceparent", out var value) && value is string s ? s : null;
    }

    private static string? FormatTraceParent(Activity? activity)
    {
        if (activity is null || activity.TraceId == default || activity.SpanId == default)
            return null;
        return $"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.ActivityTraceFlags:x2}";
    }

    private static Activity? StartTelemetryActivity(
        ProcessRequest request, bool isPriority,
        string? traceparent)
    {
        ActivityContext parentContext = default;
        if (traceparent is not null)
            ActivityContext.TryParse(traceparent, null, out parentContext);

        var activity = ActivitySource.StartActivity($"ProcessPdf {request.StartPage}-{request.EndPage}", ActivityKind.Consumer, parentContext);

        activity?.SetTag("pdf.chunk_blob", request.ChunkBlobName);
        activity?.SetTag("pdf.source_blob", request.SourceBlobName);
        activity?.SetTag("pdf.start_page", request.StartPage);
        activity?.SetTag("pdf.end_page", request.EndPage);
        activity?.SetTag("pdf.screenplay_id", request.ScreenplayId);
        activity?.SetTag("pdf.queue", isPriority ? "priority" : "standard");
        return activity;
    }

    private static void RecordTelemetryError(Exception ex)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity.Current?.AddException(ex);
    }

    internal sealed class ProcessRequest
    {
        public string ChunkBlobName { get; set; } = string.Empty;
        public string SourceBlobName { get; set; } = string.Empty;
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public Guid ScreenplayId { get; set; }
        public int TotalPages { get; set; }
        public DateTimeOffset? EnqueuedAt { get; set; }
    }
}
