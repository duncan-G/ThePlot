using Microsoft.Extensions.Hosting;

namespace ThePlot.AppHost.EnvoyProxy;

public static class EnvoyProxyResourceBuilderExtensions
{
    private const string EnvoyConfigPath = "../envoy";

    public static IResourceBuilder<ContainerResource> AddEnvoyProxy(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        var envoy = builder
            .AddDockerfile(name, EnvoyConfigPath)
            .WithHttpEndpoint(targetPort: 8080)
            .WithEntrypoint("/bin/sh")
            .WithArgs("/etc/envoy/entrypoint.sh")
            .WithEnvironment("OTEL_INSTANCE_ID", "envoy-proxy");


        if (builder.ExecutionContext.IsPublishMode)
        {
            envoy.WithEndpoint("http", e => e.IsExternal = true);
        }
        envoy
            .WithHttpEndpoint(targetPort: 9901, name: "admin", isProxied: false)
            .WithUrlForEndpoint("admin", u => u.DisplayText = "Envoy Admin")
            .WithHttpHealthCheck("/ready", statusCode: 200, endpointName: "admin");

        return envoy;
    }

    public static IResourceBuilder<ContainerResource> WithCorsOriginExact(
        this IResourceBuilder<ContainerResource> envoy,
        IDistributedApplicationBuilder applicationBuilder,
        EndpointReference clientEndpoint)
    {
        if (applicationBuilder.ExecutionContext.IsPublishMode)
        {
            // Ahtough the client is configured with HTTP endpoint, traffic is proxied
            // through Azure Container Apps environment which is HTTPS. Thus, we need to use HTTPS scheme.
            var clientHost = clientEndpoint.Property(EndpointProperty.Host);
            var clientScheme = "https";
            return envoy.WithEnvironment("CORS_ORIGIN_EXACT",
                ReferenceExpression.Create($"{clientScheme}://{clientHost}"));
        }

        return envoy.WithEnvironment("CORS_ORIGIN_EXACT", clientEndpoint);
    }

    public static IResourceBuilder<ContainerResource> WithClusterEndpoint(
        this IResourceBuilder<ContainerResource> envoy,
        IDistributedApplicationBuilder applicationBuilder,
        string name,
        EndpointReference endpoint)
    {
        envoy.WithEnvironment($"{name}_HOST", endpoint.Property(EndpointProperty.Host));
        if (applicationBuilder.Environment.IsDevelopment())
        {
            envoy.WithEnvironment($"{name}_PORT", endpoint.Property(EndpointProperty.Port));
            return envoy;
        }
        
        if (applicationBuilder.ExecutionContext.IsPublishMode)
        {
            envoy.WithEnvironment($"{name}_PORT", "443");
        }
        else
        {
            envoy.WithEnvironment($"{name}_PORT", endpoint.Property(EndpointProperty.TargetPort));
        }

        return envoy;
    }


    public static IResourceBuilder<ContainerResource> WithCorsOriginSubdomainRegex(
        this IResourceBuilder<ContainerResource> envoy,
        IDistributedApplicationBuilder applicationBuilder,
        EndpointReference clientEndpoint)
    {
        // Currently, we are not supporting CORS origin subdomain regex in ACA.
        if (applicationBuilder.ExecutionContext.IsPublishMode)
        {
            return envoy;
        }

        var clientHost = clientEndpoint.Property(EndpointProperty.HostAndPort);
        var clientScheme = clientEndpoint.Property(EndpointProperty.Scheme);
        var corsOriginSubdomainRegex = ReferenceExpression.Create($"{clientScheme}://*.{clientHost}");
        return envoy.WithEnvironment("CORS_ORIGIN_SUBDOMAIN_REGEX", corsOriginSubdomainRegex);
    }

    /// <remarks>
    /// ACA terminates TLS externally; the browser's Host header has no port, so HostAndPort (which
    /// includes the internal target port :80) would prevent Envoy's virtual-host domain matching.
    /// </remarks>
    public static IResourceBuilder<ContainerResource> WithAllowedHosts(
        this IResourceBuilder<ContainerResource> envoy,
        IDistributedApplicationBuilder applicationBuilder)
    {
        var endpointProperty = applicationBuilder.ExecutionContext.IsPublishMode
            ? EndpointProperty.Host
            : EndpointProperty.HostAndPort;

        var network = applicationBuilder.ExecutionContext.IsPublishMode
            ? KnownNetworkIdentifiers.PublicInternet
            : KnownNetworkIdentifiers.LocalhostNetwork;

        // NOTE: Env var must be comma-separated string
        return envoy.WithEnvironment("ALLOWED_HOSTS",
            envoy.GetEndpoint("http", network).Property(endpointProperty));
    }
}
