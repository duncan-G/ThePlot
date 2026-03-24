using Microsoft.Extensions.Hosting;

namespace ThePlot.AppHost.OpenTelemetryCollector;

public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string OtelConfigPath = "../otel-collector";
    private const int OtlpGrpcContainerPort = 4317;
    private const int OtlpHttpContainerPort = 4318;

    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder, string name)
    {
        var scheme = builder.Environment.IsDevelopment() ? "https" : "http";
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
            // OTLP/gRPC to the Aspire dashboard is plaintext (local URL and ACA k8se-otel:4317). Do not tie
            // this to the collector's ingress scheme — TLS to a plaintext server yields "first record does not look like a TLS handshake".
            .WithEnvironment("ASPIRE_INSECURE", "true")
            .WithEnvironment("OTLP_GRPC_PORT", OtlpGrpcContainerPort.ToString())
            .WithEnvironment("OTLP_HTTP_PORT", OtlpHttpContainerPort.ToString())
            .WithOtlpExporter();

        return resourceBuilder;
    }
}
