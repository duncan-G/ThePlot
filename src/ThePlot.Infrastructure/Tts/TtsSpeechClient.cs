using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace ThePlot.Infrastructure.Tts;

public sealed class TtsSpeechClient(HttpClient httpClient, IConfiguration configuration) : ITtsSpeechClient
{
    private const string ConnectionStringName = "tts-server";

    private const string DefaultModel = "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign";
    private const string DefaultTaskType = "VoiceDesign";
    private const string DefaultVoiceDesignInstructions =
        "A clear, natural English voice with neutral, friendly tone, suitable for narration.";

    public async Task<TtsSpeechResult> GetSpeechAsync(string userText, CancellationToken cancellationToken = default)
    {
        var (endpoint, key) = ParseConnectionString(configuration.GetConnectionString(ConnectionStringName));
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"Missing connection string '{ConnectionStringName}'.");
        }

        var taskType = configuration["Tts:TaskType"] ?? DefaultTaskType;
        var model = configuration["Tts:Model"] ?? DefaultModel;
        var responseFormat = configuration["Tts:ResponseFormat"] ?? "wav";

        var payload = new Dictionary<string, object?>
        {
            ["input"] = userText,
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
            payload["voice"] = configuration["Tts:Voice"] ?? "vivian";
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
            var detail = TryDecodeUtf8(bytes);
            throw new HttpRequestException(
                $"TTS server returned {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        if (LooksLikeJsonError(bytes))
        {
            throw new InvalidOperationException($"TTS server error: {TryDecodeUtf8(bytes)}");
        }

        var format = AudioFormatFromResponse(responseFormat, response.Content.Headers.ContentType?.MediaType);
        var audioB64 = Convert.ToBase64String(bytes);

        return new TtsSpeechResult(
            Text: userText,
            AudioBase64: audioB64,
            AudioFormat: format);
    }

    private static string AudioFormatFromResponse(string configuredFormat, string? mediaType)
    {
        if (!string.IsNullOrEmpty(configuredFormat))
        {
            return configuredFormat;
        }

        return mediaType switch
        {
            "audio/wav" => "wav",
            "audio/mpeg" => "mp3",
            "audio/flac" => "flac",
            "audio/aac" => "aac",
            "audio/opus" => "opus",
            _ => "wav",
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
