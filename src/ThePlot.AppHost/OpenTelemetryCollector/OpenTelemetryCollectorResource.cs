namespace ThePlot.AppHost.OpenTelemetryCollector;

public class OpenTelemetryCollectorResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    internal const string OtlpGrpcEndpointName = "grpc";
    internal const string OtlpHttpEndpointName = "http";
}
