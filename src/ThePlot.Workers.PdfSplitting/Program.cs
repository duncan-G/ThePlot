using ThePlot.Workers.PdfSplitting;
using Ultra.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(PdfSplittingWorker.ActivitySourceName));

builder.Services.AddHostedService<PdfChunksContainerInitializer>();
builder.Services.AddHostedService<PdfSplittingWorker>();

var host = builder.Build();
host.Run();
