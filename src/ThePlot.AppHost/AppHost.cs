using Azure.Provisioning.Storage;
using ThePlot.AppHost.BlobStorage;
using ThePlot.AppHost.ClientApp;
using ThePlot.AppHost.EnvoyProxy;
using ThePlot.AppHost.OpenTelemetryCollector;
using ThePlot.AppHost.Postgres;
using ThePlot.AppHost.VllmServer;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("theplotAcaEnv")
        .WithAzdResourceNaming();
}

var otelCollector = builder.AddOpenTelemetryCollector("otel-collector");

var grpcServer = builder.AddProject<Projects.ThePlot_Grpc_Server>("grpc-service")
    .WithHttpEndpoint(name: "grpc")
    .WithEndpoint("grpc", e =>
    {
        e.Transport = "http2";
        if (!builder.ExecutionContext.IsPublishMode)
        {
            e.TargetHost = "0.0.0.0";
        }
    })
    .WithOtlpCollectorReference(otelCollector);

IResourceBuilder<IResourceWithConnectionString> ttsServer;
IResourceBuilder<IResourceWithConnectionString> chatServer;
IResourceBuilder<IResourceWithConnectionString> embedServer;

if (builder.ExecutionContext.IsPublishMode)
{
    var foundry = builder.AddFoundry("ai-foundry");
    ttsServer = foundry.AddDeployment("tts-server", "Higgs-Audio-v2.5", "2", "BosonAI");
    chatServer = foundry.AddDeployment("chat-server", "gpt-4o-mini", "2024-07-18", "OpenAI");
    embedServer = foundry.AddDeployment("embedding-server", "text-embedding-3-small", "1", "OpenAI");
    grpcServer = grpcServer.WithReference(ttsServer);
}
else
{
    (ttsServer, chatServer, embedServer) = builder.AddVllmComposite(
        otelCollector.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName));
    grpcServer = grpcServer.WithReference(ttsServer).WaitFor(ttsServer);
}

grpcServer = grpcServer
    .WithReference(chatServer)
    .WithReference(embedServer);

var postgresDb = builder.AddDatabase("theplot-db");
var schemaMigrations = builder.WithSchemaMigrations<Projects.ThePlot_SchemaMigrations>(postgresDb, "theplot-schema-migrations");

var pdfBlobStorage = builder.AddAzureStorage("pdf-storage");
if (!builder.ExecutionContext.IsPublishMode)
{
    pdfBlobStorage = pdfBlobStorage.RunAsEmulator(emulator =>
    {
        // Don't change - these ports are Azure Storage Explorer's default emulator ports.
        // So, no need to configure anything when accessing storage explorer
        emulator.WithBlobPort(10000)
            .WithQueuePort(10001)
            .WithTablePort(10002);
    });
}
var pdfBlobs = pdfBlobStorage.AddBlobs("blobs");

var serviceBus = builder.AddAzureServiceBus("messaging");
if (!builder.ExecutionContext.IsPublishMode)
{
    serviceBus = serviceBus.RunAsEmulator();
}
serviceBus.AddServiceBusQueue("pdf-splitting-priority");
serviceBus.AddServiceBusQueue("pdf-splitting-standard");
serviceBus.AddServiceBusQueue("pdf-processing-priority");
serviceBus.AddServiceBusQueue("pdf-processing-standard");
serviceBus.AddServiceBusQueue("screenplay-import-status");

grpcServer
    .WithReference(postgresDb)
    .WithReference(pdfBlobs, "pdf-storage")
    .WithRoleAssignments(pdfBlobStorage, StorageBuiltInRole.StorageBlobDataContributor, StorageBuiltInRole.StorageBlobDelegator)
    .WithReference(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaMigrations)
    .WaitFor(serviceBus)
    .WaitFor(otelCollector);

builder.AddProject<Projects.ThePlot_Workers_PdfValidation>("pdf-validation-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithRoleAssignments(pdfBlobStorage, StorageBuiltInRole.StorageBlobDataContributor)
    .WithOtlpCollectorReference(otelCollector)
    .WithReference(postgresDb)
    .WaitFor(serviceBus)
    .WaitFor(grpcServer);

builder.AddProject<Projects.ThePlot_Workers_PdfSplitting>("pdf-splitting-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReplicas(3)
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithRoleAssignments(pdfBlobStorage, StorageBuiltInRole.StorageBlobDataContributor)
    .WithOtlpCollectorReference(otelCollector)
    .WaitFor(serviceBus)
    .WaitFor(grpcServer);

builder.AddProject<Projects.ThePlot_Workers_PdfProcessing>("pdf-processing-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReplicas(3)
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithReference(postgresDb)
    .WithOtlpCollectorReference(otelCollector)
    .WaitFor(serviceBus)
    .WaitFor(grpcServer);

builder.AddProject<Projects.ThePlot_Workers_ContentGeneration>("content-generation-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReplicas(2)
    .WithReference(postgresDb)
    .WithReference(ttsServer)
    .WithReference(chatServer)
    .WithReference(embedServer)
    .WithOtlpCollectorReference(otelCollector)
    .WaitFor(postgresDb)
    .WaitFor(schemaMigrations)
    .WaitFor(ttsServer)
    .WaitFor(chatServer)
    .WaitFor(embedServer)
    .WaitFor(grpcServer);

var envoyProxy = builder.AddEnvoyProxy("envoy-proxy")
    .WithReference(otelCollector)
    .WithReference(grpcServer)
    .WaitFor(grpcServer)
    .WaitFor(otelCollector);

var clientEndpoint = builder.AddClientApp(envoyProxy, otelCollector);

// Configure Envoy Proxy CORS & ALLOWED_HOSTS
envoyProxy
    .WithCorsOriginExact(builder, clientEndpoint)
    .WithCorsOriginSubdomainRegex(builder, clientEndpoint)
    .WithAllowedHosts(builder);

// Configure Envoy Proxy Clusters (OTEL HTTP: port from OTLP HTTP, host from OTLP gRPC for internal FQDN; published ACA uses :443 TLS)
envoyProxy
    .WithClusterEndpoint(
        builder,
        "OTEL_HTTP",
        otelCollector.GetEndpoint(OpenTelemetryCollectorResource.OtlpHttpEndpointName))
    .WithClusterEndpoint(
        builder,
        "OTEL_GRPC",
        otelCollector.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName))
    .WithClusterEndpoint(builder, "GRPC_API", grpcServer.GetEndpoint("grpc"));

pdfBlobStorage.ConfigureCorsAndLifecycle(builder, clientEndpoint);

builder.Build().Run();
