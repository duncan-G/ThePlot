using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Infrastructure.Embeddings;
using ThePlot.Infrastructure.Tts;

namespace ThePlot.Infrastructure.ContentGeneration;

public static class ContentGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddContentGenerationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContentGenerationOptions>(
            configuration.GetSection(ContentGenerationOptions.SectionName));
        services.AddScoped<IGenerationNodeClaimService, GenerationNodeClaimService>();
        services.AddScoped<IChunkReconciliationService, ChunkReconciliationService>();
        services.AddTtsSpeechClient();
        services.AddEmbeddingClient();
        return services;
    }
}
