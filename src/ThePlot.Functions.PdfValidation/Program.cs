using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using ThePlot.Functions.PdfValidation;
using ThePlot.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        services.AddSingleton(sp =>
        {
            var connectionString = config.GetConnectionString("messaging") ?? config["messaging"]
                ?? throw new InvalidOperationException("Service Bus connection string 'messaging' not found.");
            return new ServiceBusClient(connectionString);
        });

        services.AddDatabaseServices(options =>
        {
            config.GetSection("Database").Bind(options);
            options.ConnectionString = config.GetConnectionString("ust-db")
                ?? throw new InvalidOperationException("Database connection string 'ust-db' not found.");
        });

        services.Configure<WorkerOptions>(o =>
        {
            o.Capabilities["Worker.OTel.Enabled"] = bool.TrueString;
        });

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("pdf-validation-functions"))
            .UseOtlpExporter()
            .WithTracing(tracing =>
            {
                tracing.AddSource(PdfValidationFunction.ActivitySourceName);
            });
    })
    .Build();

host.Run();
