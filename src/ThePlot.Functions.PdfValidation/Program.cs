using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThePlot.Functions.PdfValidation;
using ThePlot.Infrastructure;
using ThePlot.ServiceDefaults;

// Aspire + isolated worker: use IHostApplicationBuilder (FunctionsApplication.CreateBuilder).
// HostBuilder + ConfigureFunctionsWorkerDefaults can cause host/worker mismatch and duplicate function loads.
// See https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration
var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<WorkerOptions>(o =>
{
    o.Capabilities["Worker.OTel.Enabled"] = bool.TrueString;
});

builder.AddAzureServiceBusClient("messaging");
builder.AddDatabaseServices("theplot-db", options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

var host = builder.Build();
host.Run();
