namespace ThePlot.AppHost.EnvoyProxy;

public static class EnvoyProxyResourceBuilderExtensions
{
    private const string EnvoyConfigPath = "../envoy";

    public static IResourceBuilder<ContainerResource> AddEnvoyProxy<TApiGrpc, TOtel>(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<TApiGrpc> grpcServerResource,
        IResourceBuilder<TOtel> otelCollector)
        where TApiGrpc : IResourceWithEndpoints
        where TOtel : IResourceWithEndpoints
    {
        return builder
            .AddDockerfile(name, EnvoyConfigPath)
            .WithHttpEndpoint(targetPort: 80, name: "admin", isProxied: false)
            .WithUrlForEndpoint("admin", u => u.DisplayText = "Envoy Admin")
            .WithHttpEndpoint(targetPort: 8080, isProxied: false)
            .WithEntrypoint("/bin/sh")
            .WithArgs("/etc/envoy/entrypoint.sh")
            .WithHttpHealthCheck("/ready", statusCode: 200, endpointName: "admin")
            .WithEnvironment("GRPC_ENDPOINT", grpcServerResource.GetEndpoint("grpc"))
            .WithEnvironment("OTEL_COLLECTOR_GRPC_ENDPOINT", otelCollector.GetEndpoint("grpc"))
            .WithEnvironment("OTEL_COLLECTOR_HTTP_ENDPOINT", otelCollector.GetEndpoint("http"));
    }

    public static IResourceBuilder<ContainerResource> WithCorsOriginSubdomainRegexIfDevelopment<TValue>(
        this IResourceBuilder<ContainerResource> envoy,
        IDistributedApplicationBuilder applicationBuilder,
        TValue corsOriginSubdomainRegex)
        where TValue : IValueProvider, IManifestExpressionProvider
    {
        if (applicationBuilder.ExecutionContext.IsPublishMode)
        {
            return envoy;
        }

        return envoy.WithEnvironment("CORS_ORIGIN_SUBDOMAIN_REGEX", corsOriginSubdomainRegex);
    }
}
