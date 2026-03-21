#pragma warning disable ASPIRECERTIFICATES001
using ThePlot.AppHost.OpenTelemetryCollector;

namespace ThePlot.AppHost.ClientApp;

public static class ClientAppResourceBuilderExtensions
{
    public static EndpointReference AddClientApp(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ContainerResource> envoyProxy,
        IResourceBuilder<OpenTelemetryCollectorResource> otelCollector)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            var envoyPublicHost = envoyProxy.GetEndpoint("http", KnownNetworkIdentifiers.PublicInternet).Property(EndpointProperty.Host);
            var envoyPublicUrl = ReferenceExpression.Create($"https://{envoyPublicHost}");

            var clientApp = builder.AddDockerfile("client-app", "../../client")
                .WithHttpEndpoint(targetPort: 4000, env: "PORT")
                .WithExternalHttpEndpoints()
                .WithEnvironment("SERVER_URL", envoyPublicUrl)
                .WithEnvironment("BROWSER_OTEL_ENDPOINT", ReferenceExpression.Create($"https://{envoyPublicHost}/otlp/v1"))
                .WithEnvironment("NODE_OTLP_ENDPOINT", otelCollector.GetEndpoint("http"))
                .WaitFor(envoyProxy);

            var clientEndpoint = clientApp.GetEndpoint("http", KnownNetworkIdentifiers.PublicInternet);
            clientApp.WithEnvironment("NG_ALLOWED_HOSTS", clientEndpoint.Property(EndpointProperty.Host));
            return clientEndpoint;
        }

        var clientAppDev = builder.AddJavaScriptApp("client-app", "../../client", runScriptName: "start")
            .WithHttpsEndpoint(port: 4200, env: "PORT")
            .WithUrlForEndpoint("https", u => u.DisplayText = "Client App")
            .WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.EnvironmentVariables["TLS_CERT_PATH"] = ctx.CertificatePath;
                ctx.EnvironmentVariables["TLS_KEY_PATH"] = ctx.KeyPath;
                return Task.CompletedTask;
            })
            .WithEnvironment("SERVER_URL", envoyProxy.GetEndpoint("http"))
            .WithEnvironment("BROWSER_OTEL_ENDPOINT", ReferenceExpression.Create($"{envoyProxy.GetEndpoint("http")}/otlp/v1"))
            .WithEnvironment("NODE_OTLP_ENDPOINT", otelCollector.GetEndpoint("http"))
            .WithEnvironment("NG_ALLOWED_HOSTS", "*.dev.localhost")
            .WaitFor(envoyProxy);

        return clientAppDev.GetEndpoint("https", KnownNetworkIdentifiers.LocalhostNetwork);
    }
}
