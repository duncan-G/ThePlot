using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ThePlot.Infrastructure.Embeddings;

public sealed class EmbeddingHttpClient(HttpClient httpClient, IConfiguration configuration) : IEmbeddingClient
{
    private const string ConnectionStringName = "embedding-server";

    public async Task<float[]> GetEmbeddingAsync(string text, int dimensions = 1024, CancellationToken ct = default)
    {
        var (endpoint, key) = ParseConnectionString(configuration.GetConnectionString(ConnectionStringName));
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"Missing connection string '{ConnectionStringName}'.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["input"] = text,
            ["model"] = configuration["Embedding:Model"] ?? "text-embedding",
            ["dimensions"] = dimensions,
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint.TrimEnd('/')}/v1/embeddings");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Embedding server returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Unexpected embedding response: {body}");
        }

        var embeddingArray = data[0].GetProperty("embedding");
        var result = new float[embeddingArray.GetArrayLength()];
        var idx = 0;
        foreach (var val in embeddingArray.EnumerateArray())
        {
            result[idx++] = val.GetSingle();
        }

        return result;
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
