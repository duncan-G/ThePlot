using ThePlot.AppHost.OpenTelemetryCollector;

namespace ThePlot.AppHost.VllmServer;

public static class VllmServerResourceBuilderExtensions
{
    private const string TtsServerResourceName = "tts-server";

    /// <summary>Container env key read by supervisord so the vLLM TTS process gets its own <c>OTEL_SERVICE_NAME</c>.</summary>
    private const string OtelServiceNameEnvTts = "VLLM_OTEL_SERVICE_NAME_TTS";

    /// <summary>
    /// Adds a Docker container running vLLM for TTS on :8001.
    /// Wires OTLP traces to <paramref name="otlpGrpcEndpoint"/> as
    /// <c>grpc://host:port</c> for vLLM's <c>--otlp-traces-endpoint</c>.
    /// </summary>
    public static (
        IResourceBuilder<IResourceWithConnectionString> Tts,
        IResourceBuilder<ContainerResource> Container)
        AddVllmComposite(this IDistributedApplicationBuilder builder, EndpointReference otlpGrpcEndpoint)
    {
        if (otlpGrpcEndpoint.Resource is not OpenTelemetryCollectorResource collectorResource)
        {
            throw new ArgumentException(
                "Endpoint must belong to the OpenTelemetry collector OTLP gRPC endpoint.",
                nameof(otlpGrpcEndpoint));
        }

        var tracesGrpcEndpoint = ReferenceExpression.Create(
            $"grpc://{otlpGrpcEndpoint.Property(EndpointProperty.Host)}:{otlpGrpcEndpoint.Property(EndpointProperty.Port)}");

        var container = builder
            .AddDockerfile("vllm-dev", "../ThePlot.AppHost/VllmServer")
            .WithHttpEndpoint(targetPort: 8001, name: "tts", isProxied: false)
            .WithContainerRuntimeArgs("--gpus=all")
            .WithContainerRuntimeArgs("--ipc=host")
            .WithVolume("theplot-hf-cache", "/root/.cache/huggingface")
            .WithReference(otlpGrpcEndpoint)
            .WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", tracesGrpcEndpoint)
            .WithEnvironment(
                "OTEL_EXPORTER_OTLP_TRACES_INSECURE",
                builder.ExecutionContext.IsPublishMode ? "false" : "true")
            .WithEnvironment(OtelServiceNameEnvTts, TtsServerResourceName)
            .WaitFor(builder.CreateResourceBuilder(collectorResource));

        var tts = CreateEndpointResource(builder, TtsServerResourceName, container, "tts",
            "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign");

        return (tts, container);
    }

    internal static IResourceBuilder<VllmModelEndpointResource> CreateEndpointResource(
        IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ContainerResource> container,
        string endpointName,
        string modelName,
        string basePath = "")
    {
        var resource = new VllmModelEndpointResource(name, container.Resource, endpointName, modelName, basePath);
        return builder.AddResource(resource);
    }
}
