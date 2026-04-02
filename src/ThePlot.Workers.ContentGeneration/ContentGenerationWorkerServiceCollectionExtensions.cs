using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePlot.Infrastructure.ContentGeneration;

namespace ThePlot.Workers.ContentGeneration;

public static class ContentGenerationWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddContentGenerationWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContentGenerationInfrastructure(configuration);
        services.AddScoped<VoiceDeterminationService>();
        services.AddScoped<PreGenerationAnalysisService>();
        services.AddScoped<GenerationGraphBuilder>();
        services.AddScoped<ContentGenerationRunService>();
        services.AddScoped<GenerationNodeExecutor>();
        return services;
    }
}
