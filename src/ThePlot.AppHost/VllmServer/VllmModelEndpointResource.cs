using Aspire.Hosting.ApplicationModel;

namespace ThePlot.AppHost.VllmServer;

/// <summary>
/// Thin resource that derives its connection string from a named HTTP endpoint on an
/// existing <see cref="ContainerResource"/>.  This allows a single vLLM composite
/// container (serving multiple models on different ports) to present each model as
/// a separate <see cref="IResourceWithConnectionString"/> to Aspire consumers.
/// </summary>
public sealed class VllmModelEndpointResource(
    string name,
    ContainerResource container,
    string endpointName,
    string modelName) : Resource(name), IResourceWithConnectionString, IResourceWithParent<ContainerResource>
{
    /// <inheritdoc />
    public ContainerResource Parent => container;

    private EndpointReference? _endpoint;

    public EndpointReference Endpoint =>
        _endpoint ??= new EndpointReference(container, endpointName);

    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var host = Endpoint.Property(EndpointProperty.Host);
            var port = Endpoint.Property(EndpointProperty.Port);
            var b = new ReferenceExpressionBuilder();
            b.AppendLiteral("Endpoint=http://");
            b.Append($"{host}");
            b.AppendLiteral(":");
            b.Append($"{port}");
            b.AppendLiteral(";Key=local-dev;Model=");
            b.AppendLiteral(modelName);
            return b.Build();
        }
    }
}
