#pragma warning disable MEAI001
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace ThePlot.Infrastructure.Tts;

public sealed class TtsSpeechClient(HttpClient httpClient, IConfiguration configuration) : ITextToSpeechClient
{
    private const string ConnectionStringName = "tts-server";

    private const string DefaultModel = "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign";
    private const string DefaultTaskType = "VoiceDesign";
    private const string DefaultVoiceDesignInstructions =
        "A clear, natural English voice with neutral, friendly tone, suitable for narration.";

    public TextToSpeechClientMetadata Metadata { get; } =
        new("vllm", new Uri("https://github.com/vllm-project/vllm"), DefaultModel);

    public async Task<TextToSpeechResponse> GetAudioAsync(
        string inputText,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (endpoint, key) = ParseConnectionString(configuration.GetConnectionString(ConnectionStringName));
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"Missing connection string '{ConnectionStringName}'.");
        }

        var taskType = configuration["Tts:TaskType"] ?? DefaultTaskType;
        var model = options?.ModelId ?? configuration["Tts:Model"] ?? DefaultModel;
        var responseFormat = options?.AudioFormat ?? configuration["Tts:ResponseFormat"] ?? "wav";

        var payload = new Dictionary<string, object?>
        {
            ["input"] = inputText,
            ["model"] = model,
            ["response_format"] = responseFormat,
        };

        var language = configuration["Tts:Language"];
        if (!string.IsNullOrWhiteSpace(language))
        {
            payload["language"] = language;
        }

        if (string.Equals(taskType, "VoiceDesign", StringComparison.OrdinalIgnoreCase))
        {
            payload["task_type"] = "VoiceDesign";
            var instructions = configuration["Tts:Instructions"] ?? DefaultVoiceDesignInstructions;
            payload["instructions"] = instructions;
        }
        else if (string.Equals(taskType, "CustomVoice", StringComparison.OrdinalIgnoreCase))
        {
            payload["task_type"] = "CustomVoice";
            payload["voice"] = options?.VoiceId ?? configuration["Tts:Voice"] ?? "vivian";
            if (!payload.ContainsKey("language"))
            {
                payload["language"] = "English";
            }
        }
        else if (string.Equals(taskType, "Base", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "TTS task type 'Base' requires ref_audio / ref_text; configure a different Tts:TaskType or extend TtsSpeechClient.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint.TrimEnd('/')}/v1/audio/speech");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"TTS server returned {(int)response.StatusCode}: {TryDecodeUtf8(bytes)}",
                null,
                response.StatusCode);
        }

        if (LooksLikeJsonError(bytes))
        {
            throw new InvalidOperationException($"TTS server error: {TryDecodeUtf8(bytes)}");
        }

        var mediaType = AudioMediaTypeFromResponse(responseFormat, response.Content.Headers.ContentType?.MediaType);

        return new TextToSpeechResponse([new DataContent(bytes, mediaType)]);
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string inputText,
        TextToSpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetAudioAsync(inputText, options, cancellationToken);
        foreach (var update in response.ToTextToSpeechResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType?.IsInstanceOfType(this) is true ? this : null;

    public void Dispose() { }

    private static string AudioMediaTypeFromResponse(string configuredFormat, string? mediaType)
    {
        if (!string.IsNullOrEmpty(mediaType))
        {
            return mediaType;
        }

        return configuredFormat switch
        {
            "mp3" => "audio/mpeg",
            "flac" => "audio/flac",
            "aac" => "audio/aac",
            "opus" => "audio/opus",
            _ => "audio/wav",
        };
    }

    private static bool LooksLikeJsonError(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] != (byte)'{')
        {
            return false;
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        return text.AsSpan().TrimStart().StartsWith("{\"error\"", StringComparison.Ordinal);
    }

    private static string TryDecodeUtf8(byte[] bytes)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static (string Endpoint, string Key) ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (string.Empty, string.Empty);
        }

        var endpoint = string.Empty;
        var key = string.Empty;

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[0].Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = parts[1];
            }
            else if (parts[0].Equals("Key", StringComparison.OrdinalIgnoreCase))
            {
                key = parts[1];
            }
        }

        return (endpoint, key);
    }
}
