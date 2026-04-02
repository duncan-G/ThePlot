using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThePlot.Infrastructure.Vllm;

/// <summary>
/// Coordinates which vLLM model set occupies the GPU at any given time.
/// Stops the current set via supervisord, starts the target set, and polls
/// each model's <c>/health</c> endpoint until ready.
/// </summary>
public sealed class GpuModelManager : IDisposable
{
    private readonly ISupervisordClient _supervisord;
    private readonly HttpClient _healthClient;
    private readonly IConfiguration _configuration;
    private readonly VllmOptions _options;
    private readonly ILogger<GpuModelManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private GpuModelSet _currentSet = GpuModelSet.None;

    private static readonly IReadOnlyDictionary<GpuModelSet, string[]> ProcessNames =
        new Dictionary<GpuModelSet, string[]>
        {
            [GpuModelSet.VoiceDetermination] = ["chat"],
            [GpuModelSet.Tts] = ["tts"],
        };

    private static readonly IReadOnlyDictionary<GpuModelSet, string[]> ConnectionStringNames =
        new Dictionary<GpuModelSet, string[]>
        {
            [GpuModelSet.VoiceDetermination] = ["chat-server"],
            [GpuModelSet.Tts] = ["tts-server"],
        };

    public GpuModelManager(
        ISupervisordClient supervisord,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<VllmOptions> options,
        ILogger<GpuModelManager> logger)
    {
        _supervisord = supervisord;
        _healthClient = httpClientFactory.CreateClient("vllm-health");
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    public GpuModelSet CurrentSet => _currentSet;

    public async Task EnsureLoadedAsync(GpuModelSet target, CancellationToken ct)
    {
        if (target == GpuModelSet.None)
            throw new ArgumentException("Cannot load GpuModelSet.None.", nameof(target));

        if (_currentSet == target)
            return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_currentSet == target)
                return;

            _logger.LogInformation("Swapping GPU models from {Current} to {Target}.", _currentSet, target);

            if (_currentSet != GpuModelSet.None)
            {
                foreach (var name in ProcessNames[_currentSet])
                    await _supervisord.StopProcessAsync(name, ct);
            }

            foreach (var name in ProcessNames[target])
                await _supervisord.StartProcessAsync(name, ct);

            foreach (var connStringName in ConnectionStringNames[target])
            {
                var healthUrl = GetHealthUrl(connStringName);
                if (healthUrl is not null)
                    await WaitForHealthAsync(healthUrl, connStringName, ct);
            }

            _currentSet = target;
            _logger.LogInformation("GPU models {Target} are now loaded and healthy.", target);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string? GetHealthUrl(string connectionStringName)
    {
        var raw = _configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var endpoint = ParseEndpoint(raw);
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        // vLLM exposes GET /health at the server root. Connection strings may include an API base path (e.g. /v1).
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return null;

        return $"{uri.GetLeftPart(UriPartial.Authority)}/health";
    }

    private static string? ParseEndpoint(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }

        return null;
    }

    private async Task WaitForHealthAsync(string healthUrl, string label, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + _options.ModelStartupTimeout;
        _logger.LogInformation("Waiting for {Label} health at {Url} (timeout {Timeout})...",
            label, healthUrl, _options.ModelStartupTimeout);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await _healthClient.GetAsync(healthUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("{Label} is healthy.", label);
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }

            await Task.Delay(_options.HealthCheckPollInterval, ct);
        }

        throw new TimeoutException(
            $"Model '{label}' at {healthUrl} did not become healthy within {_options.ModelStartupTimeout}.");
    }

    public void Dispose() => _lock.Dispose();
}
