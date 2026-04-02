using Microsoft.Extensions.DependencyInjection;

namespace ThePlot.Infrastructure.Embeddings;

public static class EmbeddingHttpClientExtensions
{
    public const string NamedClient = "embedding";

    public static IHttpClientBuilder AddEmbeddingClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient<IEmbeddingClient, EmbeddingHttpClient>(NamedClient, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    }
}
