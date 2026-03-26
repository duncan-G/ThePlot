using Azure.Provisioning;
using Azure.Provisioning.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThePlot.AppHost.ClientApp;
using ThePlot.AppHost.EnvoyProxy;
using ThePlot.AppHost.OpenTelemetryCollector;
using ThePlot.AppHost.Postgres;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("theplotAcaEnv")
        .WithAzdResourceNaming();
}

var otelCollector = builder.AddOpenTelemetryCollector("otel-collector");

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

var grpcServer = builder.AddProject<Projects.ThePlot_Grpc_Server>("grpc-service")
    .WithHttpEndpoint(name: "grpc")
    .WithEndpoint("grpc", e => e.Transport = "http2")
    .WithOtlpCollectorReference(otelCollector)
    .WithReference(postgresDb)
    .WithReference(pdfBlobs, "pdf-storage")
    .WithRoleAssignments(pdfBlobStorage, StorageBuiltInRole.StorageBlobDataContributor, StorageBuiltInRole.StorageBlobDelegator)
    .WithReference(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaMigrations)
    .WaitFor(serviceBus)
    .WaitFor(otelCollector);

builder.AddAzureFunctionsProject<Projects.ThePlot_Functions_PdfValidation>("pdf-validation-functions")
    .WithHostStorage(pdfBlobStorage)
    .WithReference(pdfBlobs)
    .WithRoleAssignments(pdfBlobStorage,
        StorageBuiltInRole.StorageBlobDataOwner,
        StorageBuiltInRole.StorageAccountContributor,
        StorageBuiltInRole.StorageQueueDataContributor,
        StorageBuiltInRole.StorageTableDataContributor)
    .WithOtlpCollectorReference(otelCollector)
    .WithReference(serviceBus)
    .WithReference(postgresDb)
    .WithEnvironment("AzureFunctionsJobHost__logging__logLevel__Azure.Core", "Warning")
    .WithEnvironment("AzureFunctionsJobHost__logging__logLevel__Azure.Storage", "Warning")
    .WaitFor(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaMigrations)
    .WaitFor(otelCollector);

builder.AddProject<Projects.ThePlot_Workers_PdfSplitting>("pdf-splitting-worker")
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithOtlpCollectorReference(otelCollector)
    .WaitFor(serviceBus)
    .WaitFor(otelCollector);

builder.AddProject<Projects.ThePlot_Workers_PdfProcessing>("pdf-processing-worker")
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithReference(postgresDb)
    .WithOtlpCollectorReference(otelCollector)
    .WaitFor(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaMigrations)
    .WaitFor(otelCollector);

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

// Configure blob storage CORS
if (builder.ExecutionContext.IsPublishMode)
{
    pdfBlobStorage.ConfigureInfrastructure(x =>
    {
        var storageAccount = x.GetProvisionableResources().OfType<StorageAccount>().Single();
        var blobService = new BlobService("blobService") { Parent = storageAccount };
        x.Add(blobService);

        var clientHost = clientEndpoint.Property(EndpointProperty.Host);
        var clientOrigin = ReferenceExpression.Create($"https://{clientHost}");
        var clientOriginParameter = clientOrigin.AsProvisioningParameter(x, "blobCorsClientOrigin");

        blobService.CorsRules.Add(new BicepValue<StorageCorsRule>(new StorageCorsRule
        {
            AllowedOrigins =
            [
                clientOriginParameter,
            ],
            AllowedMethods = [CorsRuleAllowedMethod.Get, CorsRuleAllowedMethod.Put, CorsRuleAllowedMethod.Options],
            AllowedHeaders = [new BicepValue<string>("*")],
            ExposedHeaders = [new BicepValue<string>("*")],
            MaxAgeInSeconds = new BicepValue<int>(3600)
        }));
    });
}
else
{
    // Configure blob storage CORS at runtime for Azurite emulator (ConfigureInfrastructure only applies to Azure provisioning)
    // See: https://github.com/dotnet/aspire/discussions/5552#discussioncomment-15239416
    pdfBlobStorage.OnResourceReady(async (_, evt, ct) =>
    {
        var logger = evt.Services.GetRequiredService<ILogger<Program>>();
        try
        {
            var ctx = new ValueProviderContext
            {
                ExecutionContext = builder.ExecutionContext,
                Network = KnownNetworkIdentifiers.LocalhostNetwork
            };
            var clientOrigin = await clientEndpoint.GetValueAsync(ctx, ct);
            var clientScheme = await clientEndpoint.Property(EndpointProperty.Scheme).GetValueAsync(ctx, ct);
            var clientHostPort = await clientEndpoint.Property(EndpointProperty.HostAndPort).GetValueAsync(ctx, ct);
            var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
            var response = await blobServiceClient.GetPropertiesAsync(cancellationToken: ct);
            var properties = response.Value;
            properties.Cors.Clear();
            properties.Cors.Add(new BlobCorsRule
            {
                AllowedOrigins = $"{clientOrigin},{clientScheme}://*.{clientHostPort}",
                AllowedMethods = "GET,PUT,OPTIONS",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600
            });
            await blobServiceClient.SetPropertiesAsync(properties, ct);
            logger.LogInformation("Configured blob storage CORS for Azurite emulator.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to configure blob storage CORS for emulator. Direct uploads may be blocked.");
        }
    });
}

builder.Build().Run();
