using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePlot.Infrastructure.ContentGeneration;

namespace ThePlot.Workers.ContentGeneration;

public static class ContentGenerationWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers services shared by both the gRPC server and the background worker
    /// (run lifecycle, node execution, infrastructure clients).
    /// </summary>
    public static IServiceCollection AddContentGenerationWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContentGenerationInfrastructure(configuration);
        services.AddScoped<ContentGenerationRunService>();
        services.AddScoped<GenerationNodeExecutor>();
        services.AddSingleton<ContentGenerationWorkPublisher>();
        return services;
    }

    /// <summary>
    /// Registers worker-only services that depend on GPU models (voice determination,
    /// graph building, analysis). Call this only in the background worker host.
    /// </summary>
    public static IServiceCollection AddContentGenerationGpuServices(
        this IServiceCollection services)
    {
        services.AddScoped<VoiceDeterminationService>();
        services.AddScoped<PreGenerationAnalysisService>();
        services.AddScoped<GenerationGraphBuilder>();
        return services;
    }
}
