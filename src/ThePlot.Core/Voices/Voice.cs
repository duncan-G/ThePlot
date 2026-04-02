using System.Text.Json;
using Pgvector;
using ThePlot.Core.Screenplays;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Voices;

public sealed class Voice : IDateStamped
{
    private Voice()
    {
    }

    public Guid Id { get; private init; }
    public Guid ScreenplayId { get; private init; }
    public string Name { get; private set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public VoiceRole Role { get; private set; }
    public string? AudioBlobUri { get; private set; }
    public string? AudioMimeType { get; private set; }
    public JsonDocument? AudioMetadata { get; private set; }
    public Vector? Embedding { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public Screenplay? Screenplay { get; private init; }

    public static Voice CreateNarrator(Guid screenplayId, string description = "Narration for action, transitions, and voice-over.")
    {
        return new Voice
        {
            Id = Guid.CreateVersion7(),
            ScreenplayId = screenplayId,
            Name = "Narrator",
            NameNormalized = "narrator",
            Description = description.Trim(),
            Role = VoiceRole.Narrator,
            AudioBlobUri = null,
            AudioMimeType = null,
            AudioMetadata = null
        };
    }

    /// <summary>One voice row per character; <see cref="NameNormalized"/> includes character id so duplicate display names do not violate unique index.</summary>
    public static Voice CreateForCharacter(
        Guid screenplayId,
        Guid characterId,
        string displayName,
        string description = "",
        string? audioBlobUri = null,
        string? audioMimeType = null,
        JsonDocument? audioMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var trimmed = displayName.Trim();
        return new Voice
        {
            Id = Guid.CreateVersion7(),
            ScreenplayId = screenplayId,
            Name = trimmed,
            NameNormalized = $"{trimmed.ToLowerInvariant()}|{characterId:D}",
            Description = description.Trim(),
            Role = VoiceRole.Character,
            AudioBlobUri = audioBlobUri,
            AudioMimeType = audioMimeType,
            AudioMetadata = audioMetadata
        };
    }

    public void SetDescription(string description) => Description = description.Trim();

    public void SetEmbedding(Vector? embedding) => Embedding = embedding;

    public void SetAudio(string? blobUri, string? mimeType, JsonDocument? metadata = null)
    {
        AudioBlobUri = blobUri;
        AudioMimeType = mimeType;
        AudioMetadata = metadata;
    }
}
