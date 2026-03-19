using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ThePlot.AppHost.OpenTelemetryCollector;

public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";
    private const string OtelConfigPath = "../otel-collector";

    public static IResourceBuilder<ContainerResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder, string name)
    {
        var url = builder.Configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
        var isHttpsEnabled = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        var dashboardOtlpEndpoint = new HostUrl(url);

        var resourceBuilder = builder
            .AddDockerfile(name, OtelConfigPath)
            .WithEndpoint(targetPort: 4317, name: OpenTelemetryCollectorResource.OtlpGrpcEndpointName, scheme: "http")
            .WithEndpoint(targetPort: 4318, name: OpenTelemetryCollectorResource.OtlpHttpEndpointName, scheme: "http")
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpHttpEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithEnvironment("ASPIRE_ENDPOINT", $"{dashboardOtlpEndpoint}")
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName])
            .WithEnvironment("ASPIRE_INSECURE", isHttpsEnabled ? "false" : "true");

        builder.Eventing.Subscribe<BeforeStartEvent>((e, ct) =>
        {
            var logger = e.Services.GetRequiredService<ILogger<OpenTelemetryCollectorResource>>();
            var appModel = e.Services.GetRequiredService<DistributedApplicationModel>();
            var collector = appModel.Resources.OfType<ContainerResource>().FirstOrDefault(r => r.Name == name);
            if (collector is null)
            {
                return Task.CompletedTask;
            }

            var endpoint = collector.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName);
            if (!endpoint.Exists)
            {
                logger.LogWarning("No {EndpointName} endpoint for the collector.", OpenTelemetryCollectorResource.OtlpGrpcEndpointName);
                return Task.CompletedTask;
            }

            // Update all resources to forward telemetry to the collector.
            foreach (var resource in appModel.Resources)
            {
                resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                {
                    if (context.EnvironmentVariables.ContainsKey(OtelExporterOtlpEndpoint))
                    {
                        logger.LogDebug("Forwarding telemetry for {ResourceName} to the collector.", resource.Name);
                        context.EnvironmentVariables[OtelExporterOtlpEndpoint] = endpoint;
                    }
                }));
            }

            return Task.CompletedTask;
        });

        if (isHttpsEnabled && builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
        {
            resourceBuilder.WithArgs("--config=/etc/otelcol-contrib/config.yaml");
        }

        return resourceBuilder;
    }
}
