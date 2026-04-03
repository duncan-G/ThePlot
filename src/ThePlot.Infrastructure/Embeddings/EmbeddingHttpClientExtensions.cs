using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace ThePlot.Infrastructure.Embeddings;

public static class EmbeddingHttpClientExtensions
{
    public const string NamedClient = "embedding";

    public static IHttpClientBuilder AddEmbeddingClient(this IServiceCollection services)
    {
        var builder = services
            .AddHttpClient<IEmbeddingClient, EmbeddingHttpClient>(NamedClient, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
        });

        return builder;
    }
}
