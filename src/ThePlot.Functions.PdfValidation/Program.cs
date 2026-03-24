using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThePlot.Functions.PdfValidation;
using ThePlot.Infrastructure;
using Ultra.ServiceDefaults;

// Aspire + isolated worker: use IHostApplicationBuilder (FunctionsApplication.CreateBuilder).
// HostBuilder + ConfigureFunctionsWorkerDefaults can cause host/worker mismatch and duplicate function loads.
// See https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration
var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<WorkerOptions>(o =>
{
    o.Capabilities["Worker.OTel.Enabled"] = bool.TrueString;
});

builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("messaging") ?? builder.Configuration["messaging"]
        ?? throw new InvalidOperationException("Service Bus connection string 'messaging' not found.");
    return new ServiceBusClient(connectionString);
});

builder.Services.AddDatabaseServices(options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
    options.ConnectionString = builder.Configuration.GetConnectionString("theplot-db")
        ?? throw new InvalidOperationException("Database connection string 'theplot-db' not found.");
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(PdfValidationFunction.ActivitySourceName);
    });

var host = builder.Build();
host.Run();
