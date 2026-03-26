using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ThePlot.Database.Abstractions;

namespace ThePlot.Functions.PdfValidation;

public class PdfValidationFunction(
    ILogger<PdfValidationFunction> logger,
    ServiceBusClient serviceBusClient,
    IScreenplayRepository screenplayRepository,
    IUnitOfWorkFactory unitOfWorkFactory)
{
    internal const string ActivitySourceName = "ThePlot.PdfValidation";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const long MaxSizeBytes = 10L * 1024 * 1024; // 10 MB

    private const string PdfContentType = "application/pdf";

    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    [Function(nameof(ValidatePdfUpload))]
    public async Task ValidatePdfUpload(
        [BlobTrigger("pdf-uploads/{name}", Connection = "AzureWebJobsStorage")] BlobClient blobClient,
        string name,
        CancellationToken cancellationToken = default)
    {
        var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var parentContext = GetParentActivityContext(props.Value.Metadata);

        using var activity = ActivitySource.StartActivity(
            "Validating PDF", 
            ActivityKind.Consumer,
            parentContext: parentContext);

        activity?.SetTag("blob.name", name);
        logger.LogInformation("Validating uploaded blob {BlobName}", name);

        await SendStatusAsync(ScreenplayImportStatusMessage.BlobUploaded(name), cancellationToken);

        var size = props.Value.ContentLength;
        var contentType = props.Value.ContentType ?? string.Empty;

        // 1. Validate Size
        if (size > MaxSizeBytes)
        {
            await RejectAndMarkFailedAsync(blobClient, name, activity, "size_exceeded", $"exceeds 10 MB limit ({size} bytes)", cancellationToken);
            return;
        }

        // 2. Validate Content Type
        if (!contentType.Equals(PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAndMarkFailedAsync(blobClient, name, activity, "invalid_content_type", $"has invalid content type '{contentType}'", cancellationToken);
            return;
        }

        // 3. Validate File Signature
        if (!await HasValidPdfSignatureAsync(blobClient, cancellationToken))
        {
            await RejectAndMarkFailedAsync(blobClient, name, activity, "invalid_signature", "does not have valid PDF signature", cancellationToken);
            return;
        }

        // Validation Passed
        activity?.SetTag("validation.result", "passed");
        activity?.SetTag("blob.size", size);
        logger.LogInformation("Blob {BlobName} passed validation ({Size} bytes, PDF).", name, size);

        await EnqueueForSplittingAsync(name, size, cancellationToken);
    }

    private async Task EnqueueForSplittingAsync(string blobName, long blobSize, CancellationToken cancellationToken)
    {
        var placeholderTitle = Path.GetFileNameWithoutExtension(blobName);
        if (string.IsNullOrWhiteSpace(placeholderTitle))
            placeholderTitle = "Untitled";

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
        }, JsonOptions));
        message.ContentType = "application/json";

        if (FormatTraceParent(Activity.Current) is { } traceparent)
        {
            message.ApplicationProperties["traceparent"] = traceparent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
        logger.LogInformation(
            "Created screenplay {ScreenplayId} and enqueued blob {BlobName} for priority splitting (pages 1-10).",
            screenplayId, blobName);
    }

    private static string? FormatTraceParent(Activity? activity)
    {
        if (activity is null || activity.TraceId == default || activity.SpanId == default)
        {
            return null;
        }
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

    private async Task RejectAndMarkFailedAsync(
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
            sbMessage.ApplicationProperties["traceparent"] = traceparent;
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
}