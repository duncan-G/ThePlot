using ThePlot.Infrastructure;
using ThePlot.ServiceDefaults;
using ThePlot.Workers.ContentGeneration;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureChatCompletionsClient("chat-server").AddChatClient();
builder.AddDatabaseServices("theplot-db", options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

builder.Services.AddContentGenerationWorkerServices(builder.Configuration);
builder.Services.AddContentGenerationGpuServices();
builder.Services.AddHostedService<ContentGenerationWorker>();

var host = builder.Build();
host.Run();
