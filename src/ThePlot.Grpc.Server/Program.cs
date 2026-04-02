using ThePlot.Infrastructure;
using ThePlot.Grpc.Server.Services;
using ThePlot.ServiceDefaults;
using ThePlot.Workers.ContentGeneration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
// Aspire's blob service health check lists containers (account scope); pdf-storage is container-scoped.
builder.AddAzureBlobServiceClient("pdf-storage", settings => settings.DisableHealthChecks = true);
builder.AddAzureChatCompletionsClient("tts-server");
builder.AddDatabaseServices("theplot-db", options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

builder.Services.AddContentGenerationWorkerServices(builder.Configuration);

builder.Services.AddSingleton<ImportStatusEventBus>();
builder.Services.AddScoped<ChunkReconciliationService>();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHostedService<PdfUploadsContainerInitializer>();
builder.Services.AddHostedService<ScreenplayImportStatusListener>();
var app = builder.Build();

app.MapGrpcService<GreeterService>();
app.MapGrpcService<UploadService>();
app.MapGrpcService<ScreenplayGrpcService>();
app.MapGrpcService<ContentGenerationGrpcService>();
app.MapGrpcHealthChecksService();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapDefaultEndpoints();

await app.RunAsync();
