#pragma warning disable ASPIRECERTIFICATES001
using Azure.Provisioning;
using Azure.Provisioning.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThePlot.AppHost;
using ThePlot.AppHost.EnvoyProxy;
using ThePlot.AppHost.OpenTelemetryCollector;
using ThePlot.AppHost.SchemaBuilder;

await AzureFunctionsCoreTools.EnsureAsync();

var builder = DistributedApplication.CreateBuilder(args);

// Required for role assignments (e.g. WithRoleAssignments on pdf-validation-functions).
// Without this, azd infra gen fails with "The application model does not support role assignments."
if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("theplot-aca-env")
        .WithAzdResourceNaming();
}

var otelCollector = builder.AddOpenTelemetryCollector("otel-collector", "../otel-collector/config.yaml");

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg18")
    .WithVolume("theplot-volume", "/var/lib/postgresql")
    .WithInitFiles("PostgresInit")
    .WithPgAdmin();
var postgresDb = postgres.AddDatabase("theplot-db");

var schemaBuilderProject = new Projects.ThePlot_SchemaBuilder();
var schemaBuilderDir = Path.GetDirectoryName(schemaBuilderProject.ProjectPath)!;
var schemaBuilder = builder.AddProject<Projects.ThePlot_SchemaBuilder>("theplot-schema-builder")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithCommand(
        "rebuild-schema",
        "Rebuild",
        context => SchemaBuilderCommands.ExecuteRebuildSchemaAsync(context, schemaBuilderDir, postgresDb.Resource),
        new CommandOptions
        {
            IconName = "ArrowClockwise",
            IconVariant = IconVariant.Filled,
            IsHighlighted = true
        });

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

var grpcServer = builder.AddProject<Projects.ThePlot_Api_Grpc>("api-grpc-service")
    .WithHttpsEndpoint(name: "grpc")
    .WithHttpsCertificateConfiguration(ctx =>
    {
        ctx.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Path"] = ctx.CertificatePath;
        ctx.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__KeyPath"] = ctx.KeyPath;
        return Task.CompletedTask;
    })
    .WithReference(postgresDb)
    .WithReference(pdfBlobs, "pdf-storage")
    .WithReference(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaBuilder)
    .WaitFor(serviceBus)
    .WaitFor(otelCollector);

builder.AddAzureFunctionsProject<Projects.ThePlot_Functions_PdfValidation>("pdf-validation-functions")
    .WithHostStorage(pdfBlobStorage)
    .WithRoleAssignments(pdfBlobStorage, StorageBuiltInRole.StorageBlobDataOwner)
    .WithReference(serviceBus)
    .WithReference(postgresDb)
    .WithEnvironment("AzureFunctionsJobHost__logging__logLevel__Azure.Core", "Warning")
    .WithEnvironment("AzureFunctionsJobHost__logging__logLevel__Azure.Storage", "Warning")
    .WaitFor(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaBuilder)
    .WaitFor(otelCollector);

builder.AddProject<Projects.ThePlot_Workers_PdfSplitting>("pdf-splitting-worker")
    .WithReplicas(3)
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WaitFor(serviceBus)
    .WaitFor(otelCollector);

builder.AddProject<Projects.ThePlot_Workers_PdfProcessing>("pdf-processing-worker")
    .WithReplicas(3)
    .WithReference(serviceBus)
    .WithReference(pdfBlobs)
    .WithReference(postgresDb)
    .WaitFor(serviceBus)
    .WaitFor(postgresDb)
    .WaitFor(schemaBuilder)
    .WaitFor(otelCollector);

var envoyProxy = builder.AddEnvoyProxy("envoy-proxy", grpcServer, otelCollector)
    .WithReference(grpcServer)
    .WaitFor(grpcServer)
    .WaitFor(otelCollector);

const string clientScheme = "https";
EndpointReference clientEndpoint;

if (builder.ExecutionContext.IsPublishMode)
{
    var envoyPublicEndpoint = envoyProxy.GetEndpoint("https", KnownNetworkIdentifiers.PublicInternet);

    var clientApp = builder.AddDockerfile("client-app", "../../client")
        .WithHttpEndpoint(targetPort: 4000, env: "PORT")
        .WithExternalHttpEndpoints()
        .WithEnvironment("SERVER_URL", envoyPublicEndpoint)
        .WithEnvironment("BROWSER_OTEL_ENDPOINT", ReferenceExpression.Create($"{envoyPublicEndpoint}/otlp/v1"))
        .WithEnvironment("NODE_OTLP_ENDPOINT", otelCollector.GetEndpoint("http"))
        .WaitFor(envoyProxy);

    clientEndpoint = clientApp.GetEndpoint("http", KnownNetworkIdentifiers.PublicInternet);
}
else
{
    var clientApp = builder.AddJavaScriptApp("client-app", "../../client", runScriptName: "start")
        .WithHttpsEndpoint(port: 4200, env: "PORT")
        .WithUrlForEndpoint("https", u => u.DisplayText = "Client App")
        .WithHttpsCertificateConfiguration(ctx =>
        {
            ctx.EnvironmentVariables["TLS_CERT_PATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["TLS_KEY_PATH"] = ctx.KeyPath;
            return Task.CompletedTask;
        })
        .WithEnvironment("SERVER_URL", envoyProxy.GetEndpoint("https"))
        .WithEnvironment("BROWSER_OTEL_ENDPOINT", ReferenceExpression.Create($"{envoyProxy.GetEndpoint("https")}/otlp/v1"))
        .WithEnvironment("NODE_OTLP_ENDPOINT", otelCollector.GetEndpoint("http"))
        .WithEnvironment("NG_ALLOWED_HOSTS", "*.dev.localhost")
        .WaitFor(envoyProxy);

    clientEndpoint = clientApp.GetEndpoint("https", KnownNetworkIdentifiers.LocalhostNetwork);
}

var clientHostAndPort = clientEndpoint.Property(EndpointProperty.HostAndPort);
envoyProxy.WithEnvironment("CORS_ORIGIN_EXACT", clientEndpoint);
envoyProxy.WithEnvironment("CORS_ORIGIN_SUBDOMAIN_REGEX", $"{clientScheme}://*.{clientHostAndPort}");

var envoyNetwork = builder.ExecutionContext.IsPublishMode
    ? KnownNetworkIdentifiers.PublicInternet
    : KnownNetworkIdentifiers.LocalhostNetwork;
envoyProxy.WithEnvironment("ALLOWED_HOSTS", envoyProxy.GetEndpoint("https", envoyNetwork).Property(EndpointProperty.HostAndPort));

// Configure blob storage CORS at runtime for Azurite emulator (ConfigureInfrastructure only applies to Azure provisioning)
// See: https://github.com/dotnet/aspire/discussions/5552#discussioncomment-15239416
if (builder.Environment.IsDevelopment())
{
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
            var clientHostPort = await clientHostAndPort.GetValueAsync(ctx, ct);
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
else
{
    pdfBlobStorage.ConfigureInfrastructure(x =>
    {
        var blobStorage = x.GetProvisionableResources().OfType<BlobService>().Single();
        blobStorage.CorsRules.Add(new BicepValue<StorageCorsRule>(new StorageCorsRule
        {
            AllowedOrigins =
            [
                new BicepValue<string>($"{clientEndpoint}"),
                new BicepValue<string>($"{clientScheme}://*.{clientHostAndPort}")
            ],
            AllowedMethods = [CorsRuleAllowedMethod.Get, CorsRuleAllowedMethod.Put, CorsRuleAllowedMethod.Options],
            AllowedHeaders = [new BicepValue<string>("*")],
            ExposedHeaders = [new BicepValue<string>("*")],
            MaxAgeInSeconds = new BicepValue<int>(3600)
        }));
    });
}

builder.Build().Run();
