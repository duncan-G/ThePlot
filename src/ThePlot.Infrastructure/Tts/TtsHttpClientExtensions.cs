using Microsoft.Extensions.DependencyInjection;

namespace ThePlot.Infrastructure.Tts;

public static class TtsHttpClientExtensions
{
    public const string NamedClient = "tts-speech";

    public static IHttpClientBuilder AddTtsSpeechClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient<ITtsSpeechClient, TtsSpeechClient>(NamedClient, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    }
}
