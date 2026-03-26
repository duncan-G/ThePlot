using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MuPDF.NET;
using ThePlot.Core.ScreenplayImports;

namespace ThePlot.Workers.PdfSplitting;

public class PdfSplittingWorker(
    ILogger<PdfSplittingWorker> logger,
    ServiceBusClient serviceBusClient,
    BlobServiceClient blobServiceClient,
    IConfiguration configuration) : BackgroundService
{
    internal const string ActivitySourceName = "ThePlot.PdfSplitting";
    public const string PriorityQueue = "pdf-splitting-priority";
    public const string StandardQueue = "pdf-splitting-standard";
    public const string ProcessingPriorityQueue = "pdf-processing-priority";
    public const string ProcessingStandardQueue = "pdf-processing-standard";

    private const string UploadsContainer = "pdf-uploads";
    private const string ChunksContainer = "pdf-chunks";
    private const int PagesPerChunk = 10;
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
            "PdfSplittingWorker starting. Priority: {PriorityQueue}, Standard: {StandardQueue}",
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
        SplitRequest? request = null;

        try
        {
            request = DeserializeRequest(args.Message);
            var traceparent = GetTraceParent(args.Message);
            var processingTraceparent = GetProcessingTraceparent(args.Message);
            using var activity = StartTelemetryActivity(request, isPriority, traceparent);

            logger.LogInformation(
                "Processing {Queue} split: blob={BlobName}, pages {Start}-{End}",
                isPriority ? "priority" : "standard", request.BlobName, request.StartPage, request.EndPage);

            var timeoutSeconds = configuration.GetValue("PdfSplitting:ChunkTimeoutSeconds", DefaultChunkTimeoutSeconds);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await ExecuteSplitWorkflowAsync(request, isPriority, activity, processingTraceparent, timeoutCts.Token);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (OperationCanceledException ex) when (args.CancellationToken.IsCancellationRequested == false)
        {
            RecordTelemetryError(ex);
            logger.LogWarning(ex, "Chunk split timed out for blob {BlobName}", request?.BlobName ?? "unknown");
            await SetImportFailedAndDeadLetterAsync(request?.ScreenplayId, "Chunk processing timed out", args);
        }
        catch (PdfSplitException ex)
        {
            RecordTelemetryError(ex);
            logger.LogError(ex, "Non-retriable split failure for blob {BlobName}", request?.BlobName ?? "unknown");
            await SetImportFailedAndDeadLetterAsync(request?.ScreenplayId, ex.Message, args);
        }
        catch (Exception ex)
        {
            RecordTelemetryError(ex);
            logger.LogError(ex, "Failed to process split for blob {BlobName}", request?.BlobName ?? "unknown");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private async Task SetImportFailedAndDeadLetterAsync(Guid? screenplayId, string message, ProcessMessageEventArgs args)
    {
        try
        {
            await SendStatusAsync(ScreenplayImportStatusMessage.ImportFailed(screenplayId, null, message), args.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send ImportFailed status before dead-lettering");
        }

        await args.DeadLetterMessageAsync(args.Message, "PdfSplitFailed", message, args.CancellationToken);
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

    private Task HandleProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, Namespace: {Namespace}, Entity: {Entity}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
        return Task.CompletedTask;
    }

    private async Task ExecuteSplitWorkflowAsync(
        SplitRequest request,
        bool isPriority,
        Activity? activity,
        string? processingTraceparent,
        CancellationToken ct)
    {
        var pdfBytes = await DownloadBlobAsync(UploadsContainer, request.BlobName, ct);

        Document sourceDoc;
        try
        {
            sourceDoc = new Document(stream: pdfBytes, fileType: "pdf");
        }
        catch (Exception ex)
        {
            throw new PdfSplitException($"PDF could not be opened: {ex.Message}", ex);
        }

        try
        {
            var pageCount = sourceDoc.PageCount;
            var startIdx = Math.Max(0, request.StartPage - 1);
            var endIdx = Math.Min(pageCount, request.EndPage) - 1;

            activity?.SetTag("pdf.source_page_count", pageCount);

            if (IsOutOfBounds(startIdx, endIdx, pageCount))
            {
                logger.LogWarning(
                    "Page range {Start}-{End} out of bounds (document has {PageCount} pages). Completing message.",
                    request.StartPage, request.EndPage, pageCount);
                
                activity?.SetTag("pdf.result", "out_of_bounds");
                return;
            }

            byte[] chunkBytes;
            try
            {
                chunkBytes = ExtractPageRange(sourceDoc, startIdx, endIdx);
            }
            catch (Exception ex)
            {
                throw new PdfSplitException($"PDF page extraction failed: {ex.Message}", ex);
            }

            var chunkBlobName = GenerateChunkBlobName(request.BlobName, request.StartPage, endIdx + 1);

            await UploadAndOptionallyQueueAsync(request, chunkBlobName, chunkBytes, pageCount, isPriority, processingTraceparent, ct);

            await SendStatusAsync(ScreenplayImportStatusMessage.ChunkSplitDone(
                request.ScreenplayId, request.StartPage, request.EndPage, pageCount, isPriority), ct);

            activity?.SetTag("pdf.result", "completed");
            activity?.SetTag("pdf.chunk_blob", chunkBlobName);
            
            logger.LogInformation(
                "Completed split: {ChunkBlob} ({PageCount} total pages in source)",
                chunkBlobName, pageCount);
        }
        finally
        {
            sourceDoc.Close();
        }
    }

    private async Task UploadAndOptionallyQueueAsync(
        SplitRequest request,
        string chunkBlobName,
        byte[] chunkBytes,
        int pageCount,
        bool isPriority,
        string? processingTraceparent,
        CancellationToken ct)
    {
        await UploadChunkAsync(chunkBlobName, chunkBytes, ct);

        var processTraceparent = isPriority
            ? FormatTraceParent(Activity.Current)
            : processingTraceparent;

        if (isPriority)
        {
            await EnqueueProcessingAsync(chunkBlobName, request, pageCount, isPriority, processTraceparent, ct);
            await EnqueueRemainingChunksAsync(request, pageCount, ct);
        }
        else
        {
            await EnqueueProcessingAsync(chunkBlobName, request, pageCount, isPriority, processTraceparent, ct);
        }
    }

    private async Task EnqueueProcessingAsync(
        string chunkBlobName,
        SplitRequest request,
        int totalPages,
        bool isPriority,
        string? traceparent,
        CancellationToken ct)
    {
        var queueName = isPriority ? ProcessingPriorityQueue : ProcessingStandardQueue;
        await using var sender = serviceBusClient.CreateSender(queueName);

        var processRequest = new ProcessRequest
        {
            ChunkBlobName = chunkBlobName,
            SourceBlobName = request.BlobName,
            StartPage = request.StartPage,
            EndPage = request.EndPage,
            ScreenplayId = request.ScreenplayId,
            TotalPages = totalPages,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(processRequest, JsonOptions))
        {
            ContentType = "application/json"
        };

        if (traceparent is not null)
            message.ApplicationProperties["traceparent"] = traceparent;

        await sender.SendMessageAsync(message, ct);
        logger.LogInformation("Enqueued {Queue} processing for chunk {ChunkBlob}",
            isPriority ? "priority" : "standard", chunkBlobName);
    }

    private static byte[] ExtractPageRange(Document sourceDoc, int startIdx, int endIdx)
    {
        var chunkDoc = new Document();
        try
        {
            chunkDoc.InsertPdf(sourceDoc, fromPage: startIdx, toPage: endIdx);
            return chunkDoc.Write(garbage: true, deflate: true);
        }
        finally
        {
            chunkDoc.Close();
        }
    }

    private async Task<byte[]> DownloadBlobAsync(string containerName, string blobName, CancellationToken ct)
    {
        var container = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = container.GetBlobClient(blobName);
        
        using var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms, ct);
        
        return ms.ToArray();
    }

    private async Task UploadChunkAsync(string chunkBlobName, byte[] chunkBytes, CancellationToken ct)
    {
        var container = blobServiceClient.GetBlobContainerClient(ChunksContainer);
        var blobClient = container.GetBlobClient(chunkBlobName);
        
        await using var stream = new MemoryStream(chunkBytes);
        await blobClient.UploadAsync(
            stream, 
            new BlobHttpHeaders { ContentType = "application/pdf" }, 
            cancellationToken: ct);
    }

    private async Task EnqueueRemainingChunksAsync(SplitRequest splitRequest, int pageCount, CancellationToken ct)
    {
        if (pageCount <= PagesPerChunk)
        {
            return;
        }

        var firstPagesContext = Activity.Current?.Context;
        ActivityLink[] links = firstPagesContext is { } ctx ? [new ActivityLink(ctx)] : [];

        var previousActivity = Activity.Current;
        Activity.Current = null;
        try
        {
            using var remainingTrace = ActivitySource.StartActivity(
                "SplitPdf RemainingPages",
                ActivityKind.Producer,
                parentContext: default,
                links: links);

            remainingTrace?.SetTag("pdf.blob_name", splitRequest.BlobName);
            remainingTrace?.SetTag("pdf.total_pages", pageCount);

            Activity.Current = null;

            using var processRemainingTrace = ActivitySource.StartActivity(
                "ProcessPdf Remaining Pages",
                ActivityKind.Producer,
                parentContext: default,
                links: links);

            processRemainingTrace?.SetTag("pdf.blob_name", splitRequest.BlobName);
            processRemainingTrace?.SetTag("pdf.total_pages", pageCount);

            await using var sender = serviceBusClient.CreateSender(StandardQueue);
            var splitTraceparent = FormatTraceParent(remainingTrace);
            var processingTraceparent = FormatTraceParent(processRemainingTrace);
            var messages = CreateRemainingChunkMessages(splitRequest, pageCount, splitTraceparent, processingTraceparent);

            await sender.SendMessagesAsync(messages, ct);
            logger.LogInformation("Enqueued {Count} standard split messages for {BlobName} ({PageCount} pages)",
                messages.Count, splitRequest.BlobName, pageCount);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    private static List<ServiceBusMessage> CreateRemainingChunkMessages(
        SplitRequest splitRequest, int pageCount,
        string? traceparent, string? processingTraceparent)
    {
        var messages = new List<ServiceBusMessage>();

        for (var startPage = PagesPerChunk + 1; startPage <= pageCount; startPage += PagesPerChunk)
        {
            var endPage = Math.Min(startPage + PagesPerChunk - 1, pageCount);
            
            var request = new SplitRequest
            {
                BlobName = splitRequest.BlobName,
                BlobSize = splitRequest.BlobSize,
                StartPage = startPage,
                EndPage = endPage,
                ScreenplayId = splitRequest.ScreenplayId,
                EnqueuedAt = DateTimeOffset.UtcNow
            };

            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(request, JsonOptions))
            {
                ContentType = "application/json"
            };
            if (traceparent is not null)
                message.ApplicationProperties["traceparent"] = traceparent;
            if (processingTraceparent is not null)
                message.ApplicationProperties["processing-traceparent"] = processingTraceparent;
            messages.Add(message);
        }

        return messages;
    }

    private static SplitRequest DeserializeRequest(ServiceBusReceivedMessage message)
    {
        return message.Body.ToObjectFromJson<SplitRequest>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize split request");
    }

    private static string? GetTraceParent(ServiceBusReceivedMessage message)
    {
        return message.ApplicationProperties.TryGetValue("traceparent", out var value) && value is string s ? s : null;
    }

    private static string? GetProcessingTraceparent(ServiceBusReceivedMessage message)
    {
        return message.ApplicationProperties.TryGetValue("processing-traceparent", out var value) && value is string s ? s : null;
    }

    private static string? FormatTraceParent(Activity? activity)
    {
        if (activity is null || activity.TraceId == default || activity.SpanId == default)
        {
            return null;
        }
        return $"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.ActivityTraceFlags:x2}";
    }

    private static Activity? StartTelemetryActivity(SplitRequest request, bool isPriority, string? traceparent)
    {
        ActivityContext parentContext = default;
        if (traceparent is not null)
            ActivityContext.TryParse(traceparent, null, out parentContext);

        var activity = ActivitySource.StartActivity($"SplitPdf {request.StartPage}-{request.EndPage}", ActivityKind.Consumer, parentContext);

        activity?.SetTag("pdf.blob_name", request.BlobName);
        activity?.SetTag("pdf.start_page", request.StartPage);
        activity?.SetTag("pdf.end_page", request.EndPage);
        activity?.SetTag("pdf.queue", isPriority ? "priority" : "standard");
        return activity;
    }

    private static void RecordTelemetryError(Exception ex)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity.Current?.AddException(ex);
    }

    private static bool IsOutOfBounds(int startIdx, int endIdx, int pageCount)
    {
        return startIdx > endIdx || startIdx >= pageCount;
    }

    private static string GenerateChunkBlobName(string blobName, int startPage, int endPage)
    {
        var fileName = Path.GetFileNameWithoutExtension(blobName);
        return $"chunks/{fileName}/pages-{startPage}-{endPage}.pdf";
    }

    private sealed class SplitRequest
    {
        public string BlobName { get; set; } = string.Empty;
        public long BlobSize { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public Guid ScreenplayId { get; set; }
        public DateTimeOffset? EnqueuedAt { get; set; }
    }

    private sealed class ProcessRequest
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

internal sealed class PdfSplitException(string message, Exception? inner = null) : Exception(message, inner);