using System.Text.Json;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public sealed class GeneratedArtifact : IDateStamped
{
    private GeneratedArtifact()
    {
    }

    public Guid Id { get; private init; }
    public Guid GenerationNodeId { get; private init; }
    public Guid GenerationAttemptId { get; private init; }
    public bool IsCurrent { get; private set; }
    public string? StorageUri { get; private init; }
    public string? MimeType { get; private init; }
    public string? ContentHash { get; private init; }
    public JsonDocument? Metadata { get; private init; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public GenerationNode? GenerationNode { get; private init; }
    public GenerationAttempt? GenerationAttempt { get; private init; }

    public static GeneratedArtifact Create(
        Guid generationNodeId,
        Guid generationAttemptId,
        bool isCurrent,
        string? storageUri,
        string? mimeType,
        string? contentHash,
        JsonDocument? metadata = null)
    {
        return new GeneratedArtifact
        {
            Id = Guid.CreateVersion7(),
            GenerationNodeId = generationNodeId,
            GenerationAttemptId = generationAttemptId,
            IsCurrent = isCurrent,
            StorageUri = storageUri,
            MimeType = mimeType,
            ContentHash = contentHash,
            Metadata = metadata
        };
    }

    public void Supersede() => IsCurrent = false;
}
