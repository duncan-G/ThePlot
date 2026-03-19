#pragma warning disable ASPIRECERTIFICATES001
namespace ThePlot.AppHost.EnvoyProxy;

public static class EnvoyProxyResourceBuilderExtensions
{
    private const string ImageName = "envoyproxy/envoy";
    private const string ImageTag = "v1.34-latest";
    private const string EnvoyConfigPath = "../envoy";

    public static IResourceBuilder<ContainerResource> AddEnvoyProxy<TApiGrpc, TOtel>(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<TApiGrpc> grpcServerResource,
        IResourceBuilder<TOtel> otelCollector,
        string envoyConfigPath = EnvoyConfigPath)
        where TApiGrpc : IResourceWithEndpoints
        where TOtel : IResourceWithEndpoints
    {
        // In publish mode, use custom image with entrypoint.sh and envoy.yaml.tmpl baked in.
        // Azure File volume is empty, so bind mount content is not available in production.
        var resource = builder.ExecutionContext.IsPublishMode
            ? builder.AddDockerfile(name, EnvoyConfigPath)
            : builder.AddContainer(name, ImageName, ImageTag)
                .WithBindMount(envoyConfigPath, "/etc/envoy", isReadOnly: true);

        return resource
            .WithHttpEndpoint(targetPort: 80, name: "admin", isProxied: false)
            .WithUrlForEndpoint("admin", u => u.DisplayText = "Envoy Admin")
            .WithHttpsEndpoint(targetPort: 8080, isProxied: false)
            .WithEntrypoint("/bin/sh")
            .WithArgs("/etc/envoy/entrypoint.sh")
            .WithHttpHealthCheck("/ready", statusCode: 200, endpointName: "admin")
            .WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.EnvironmentVariables["TLS_CERT_PATH"] = ctx.CertificatePath;
                ctx.EnvironmentVariables["TLS_KEY_PATH"] = ctx.KeyPath;
                return Task.CompletedTask;
            })
            .WithEnvironment("GRPC_ENDPOINT", grpcServerResource.GetEndpoint("grpc"))
            .WithEnvironment("OTEL_COLLECTOR_GRPC_ENDPOINT", otelCollector.GetEndpoint("grpc"))
            .WithEnvironment("OTEL_COLLECTOR_HTTP_ENDPOINT", otelCollector.GetEndpoint("http"));
    }
}
