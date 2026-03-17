using OpenTelemetry.Trace;
using ThePlot.Infrastructure;
using ThePlot.Workers.PdfProcessing;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddDatabaseServices(options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
    var connectionString = builder.Configuration.GetConnectionString("ust-db")
        ?? throw new ArgumentException("Database connection string is invalid.");
    options.ConnectionString = connectionString;
});

builder.Services.AddScoped<ScreenplayPersistenceService>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(PdfProcessingWorker.ActivitySourceName)
        .AddEntityFrameworkCoreInstrumentation());

builder.Services.AddHostedService<PdfProcessingWorker>();

var host = builder.Build();
host.Run();
