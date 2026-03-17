using ThePlot.Infrastructure;
using ThePlot.Api.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");

builder.Services.AddDatabaseServices(options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
    var connectionString = builder.Configuration.GetConnectionString("ust-db")
        ?? throw new ArgumentException("Database connection string is invalid.");
    options.ConnectionString = connectionString;
});

builder.Services.AddSingleton<ImportStatusEventBus>();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHostedService<PdfUploadsContainerInitializer>();
builder.Services.AddHostedService<ScreenplayImportStatusListener>();

var app = builder.Build();

app.MapGrpcService<GreeterService>();
app.MapGrpcService<UploadService>();
app.MapGrpcService<ScreenplayGrpcService>();
app.MapGrpcHealthChecksService();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapDefaultEndpoints();

await app.RunAsync();
