using OpenTelemetry.Trace;
using ThePlot.Infrastructure;
using ThePlot.Workers.PdfProcessing;
using ThePlot.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureNpgsqlDataSource("theplot-db", configureDataSourceBuilder: dsb => dsb.ConfigureVectorTypes());
builder.Services.AddDatabaseServices(options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

builder.Services.AddScoped<ScreenplayPersistenceService>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(PdfProcessingWorker.ActivitySourceName)
        .AddEntityFrameworkCoreInstrumentation());

builder.Services.AddHostedService<PdfProcessingWorker>();

var host = builder.Build();
host.Run();
