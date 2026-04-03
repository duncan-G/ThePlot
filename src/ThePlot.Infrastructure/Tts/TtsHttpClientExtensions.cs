#pragma warning disable MEAI001
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace ThePlot.Infrastructure.Tts;

public static class TtsHttpClientExtensions
{
    public const string NamedClient = "tts-speech";

    public static IServiceCollection AddTtsSpeechClient(this IServiceCollection services)
    {
        services
            .AddHttpClient<TtsSpeechClient>(NamedClient, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(30);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
            });

        services
            .AddTextToSpeechClient(sp =>
            {
                var client = sp.GetRequiredService<TtsSpeechClient>();
                client.EnableSensitiveData = true;
                return client;
            })
            .UseOpenTelemetry(configure: o => o.EnableSensitiveData = true);

        return services;
    }
}
