using Microsoft.Extensions.Hosting;

namespace ThePlot.AppHost.OpenTelemetryCollector;

public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string OtelConfigPath = "../otel-collector";
    private const int OtlpGrpcContainerPort = 4317;
    private const int OtlpHttpContainerPort = 4318;
    private const string scheme = "http";

    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder, string name)
    {
        var otlpApiKey = builder.Configuration[DashboardOtlpApiKeyVariableName] ?? string.Empty;

        var collectorResource = new OpenTelemetryCollectorResource(name);

        var resourceBuilder = builder
            .AddResource(collectorResource)
            .WithImage("placeholder") // Replaced when the image is built from the Dockerfile (same as AddDockerfile).
            .WithDockerfile(OtelConfigPath)
            .WithEndpoint(
                targetPort: OtlpGrpcContainerPort,
                name: OpenTelemetryCollectorResource.OtlpGrpcEndpointName,
                scheme: scheme)
            .WithEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName, e => e.Transport = "http2")
            .WithEndpoint(
                targetPort: OtlpHttpContainerPort,
                name: OpenTelemetryCollectorResource.OtlpHttpEndpointName,
                scheme: scheme)
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpHttpEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithEnvironment("ASPIRE_API_KEY", otlpApiKey)
            .WithEnvironment("ASPIRE_INSECURE", "true")
            .WithEnvironment("OTLP_GRPC_PORT", OtlpGrpcContainerPort.ToString())
            .WithEnvironment("OTLP_HTTP_PORT", OtlpHttpContainerPort.ToString())
            .WithOtlpExporter();

        return resourceBuilder;
    }

    /// <summary>
    /// Overrides <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> to route telemetry through the collector
    /// instead of directly to the Aspire dashboard. The collector forwards to the dashboard
    /// via its own <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (set by Aspire on the collector container).
    /// </summary>
    public static IResourceBuilder<T> WithOtlpCollectorReference<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<OpenTelemetryCollectorResource> otelCollector)
        where T : IResourceWithEnvironment
    {
        return builder
            .WithReference(otelCollector)
            .WithEnvironment(
                "OTEL_EXPORTER_OTLP_ENDPOINT",
                otelCollector.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName));
    }
}
