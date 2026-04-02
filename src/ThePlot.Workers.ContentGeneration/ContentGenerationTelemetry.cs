using System.Diagnostics;

namespace ThePlot.Workers.ContentGeneration;

public static class ContentGenerationTelemetry
{
    public const string ActivitySourceName = "ThePlot.ContentGeneration";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Starts an activity, optionally re-parenting it under a stored W3C traceparent so that
    /// work performed across async boundaries (worker cycles, gRPC calls) appears in the same trace.
    /// When <paramref name="traceParent"/> is <c>null</c>, the ambient <see cref="Activity.Current"/> is used.
    /// </summary>
    public static Activity? StartActivity(string name, string? traceParent = null)
    {
        if (traceParent is not null && ActivityContext.TryParse(traceParent, null, out var parentContext))
            return Source.StartActivity(name, ActivityKind.Internal, parentContext);

        return Source.StartActivity(name, ActivityKind.Internal);
    }

    public static string? FormatTraceParent(Activity? activity)
    {
        if (activity is null || activity.TraceId == default || activity.SpanId == default)
            return null;
        return $"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.ActivityTraceFlags:x2}";
    }

    public static void RecordError(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
    }
}
