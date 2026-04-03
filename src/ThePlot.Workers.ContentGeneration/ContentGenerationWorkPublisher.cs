using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace ThePlot.Workers.ContentGeneration;

public sealed class ContentGenerationWorkPublisher(ServiceBusClient serviceBusClient)
{
    public const string QueueName = "content-generation-work";

    public async Task PublishVoiceDeterminationAsync(Guid runId, string? traceParent, CancellationToken ct)
    {
        var message = new ContentGenerationWorkMessage
        {
            Kind = ContentGenerationWorkMessage.VoiceDetermination,
            RunId = runId,
            TraceParent = traceParent,
        };
        await SendAsync(message, ct);
    }

    public async Task PublishTtsWorkAvailableAsync(Guid? runId, string? traceParent, CancellationToken ct)
    {
        var message = new ContentGenerationWorkMessage
        {
            Kind = ContentGenerationWorkMessage.TtsWorkAvailable,
            RunId = runId,
            TraceParent = traceParent,
        };
        await SendAsync(message, ct);
    }

    private async Task SendAsync(ContentGenerationWorkMessage message, CancellationToken ct)
    {
        await using var sender = serviceBusClient.CreateSender(QueueName);
        var sbMessage = new ServiceBusMessage(
            BinaryData.FromObjectAsJson(message, ContentGenerationWorkMessage.JsonOptions))
        {
            ContentType = "application/json",
        };

        var traceParent = message.TraceParent
            ?? ContentGenerationTelemetry.FormatTraceParent(Activity.Current);
        if (traceParent is not null)
        {
            sbMessage.ApplicationProperties["traceparent"] = traceParent;
        }

        await sender.SendMessageAsync(sbMessage, ct);
    }
}
