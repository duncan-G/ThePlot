using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThePlot.Workers.ContentGeneration;

public sealed record ContentGenerationWorkMessage
{
    public const string VoiceDetermination = nameof(VoiceDetermination);
    public const string TtsWorkAvailable = nameof(TtsWorkAvailable);

    public required string Kind { get; init; }
    public Guid? RunId { get; init; }
    public string? TraceParent { get; init; }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
