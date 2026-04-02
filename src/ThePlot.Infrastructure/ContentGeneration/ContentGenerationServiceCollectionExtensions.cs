using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<GenerationNodeClaimService>();
        services.AddTtsSpeechClient();
        services.AddEmbeddingClient();
        return services;
    }
}
