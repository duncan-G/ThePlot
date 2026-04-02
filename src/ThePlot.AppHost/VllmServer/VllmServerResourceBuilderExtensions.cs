using ThePlot.AppHost.OpenTelemetryCollector;

namespace ThePlot.AppHost.VllmServer;

public static class VllmServerResourceBuilderExtensions
{
    private const string TtsServerResourceName = "tts-server";
    private const string ChatServerResourceName = "chat-server";
    private const string EmbeddingServerResourceName = "embedding-server";

    /// <summary>Container env keys read by supervisord so each vLLM process gets its own <c>OTEL_SERVICE_NAME</c>.</summary>
    private const string OtelServiceNameEnvTts = "VLLM_OTEL_SERVICE_NAME_TTS";
    private const string OtelServiceNameEnvChat = "VLLM_OTEL_SERVICE_NAME_CHAT";
    private const string OtelServiceNameEnvEmbedding = "VLLM_OTEL_SERVICE_NAME_EMBEDDING";

    /// <summary>
    /// Adds a single Docker container running supervisord with three vLLM processes
    /// (TTS on :8001, Chat on :8002, Embedding on :8003). Wires OTLP traces to
    /// <paramref name="otlpGrpcEndpoint"/> as <c>grpc://host:port</c> for vLLM's
    /// <c>--otlp-traces-endpoint</c>. <paramref name="otlpGrpcEndpoint"/> must be the collector's gRPC OTLP endpoint.
    /// </summary>
    public static (
        IResourceBuilder<IResourceWithConnectionString> Tts,
        IResourceBuilder<IResourceWithConnectionString> Chat,
        IResourceBuilder<IResourceWithConnectionString> Embedding)
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
            .WithHttpEndpoint(targetPort: 8002, name: "chat", isProxied: false)
            .WithHttpEndpoint(targetPort: 8003, name: "embedding", isProxied: false)
            .WithContainerRuntimeArgs("--gpus=all")
            .WithContainerRuntimeArgs("--ipc=host")
            .WithVolume("theplot-hf-cache", "/root/.cache/huggingface")
            .WithReference(otlpGrpcEndpoint)
            .WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", tracesGrpcEndpoint)
            .WithEnvironment(
                "OTEL_EXPORTER_OTLP_TRACES_INSECURE",
                builder.ExecutionContext.IsPublishMode ? "false" : "true")
            .WithEnvironment(OtelServiceNameEnvTts, TtsServerResourceName)
            .WithEnvironment(OtelServiceNameEnvChat, ChatServerResourceName)
            .WithEnvironment(OtelServiceNameEnvEmbedding, EmbeddingServerResourceName)
            .WaitFor(builder.CreateResourceBuilder(collectorResource));

        var tts = CreateEndpointResource(builder, TtsServerResourceName, container, "tts",
            "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign");
        var chatServer = CreateEndpointResource(builder, ChatServerResourceName, container, "chat",
            "Qwen/Qwen3-0.6B");
        var embedServer = CreateEndpointResource(builder, EmbeddingServerResourceName, container, "embedding",
            "Qwen/Qwen3-Embedding-0.6B");

        return (tts, chatServer, embedServer);
    }

    private static IResourceBuilder<VllmModelEndpointResource> CreateEndpointResource(
        IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ContainerResource> container,
        string endpointName,
        string modelName)
    {
        var resource = new VllmModelEndpointResource(name, container.Resource, endpointName, modelName);
        return builder.AddResource(resource);
    }
}
