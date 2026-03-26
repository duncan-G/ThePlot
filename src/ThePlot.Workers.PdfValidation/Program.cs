using ThePlot.Infrastructure;
using ThePlot.ServiceDefaults;
using ThePlot.Workers.PdfValidation;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");
builder.AddDatabaseServices("theplot-db", options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

builder.Services.AddHostedService<PdfValidationWorker>();

var host = builder.Build();
host.Run();
