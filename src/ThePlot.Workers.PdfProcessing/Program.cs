using OpenTelemetry.Trace;
using ThePlot.Infrastructure;
using ThePlot.Workers.PdfProcessing;
using ThePlot.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");
builder.AddDatabaseServices("theplot-db", options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

builder.Services.AddScoped<ScreenplayPersistenceService>();

builder.Services.AddHostedService<PdfProcessingWorker>();

var host = builder.Build();
host.Run();
