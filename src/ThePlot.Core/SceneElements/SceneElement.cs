using System.Text.Json;
using Pgvector;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.SceneElements;

public sealed class SceneElement : IDateStamped
{
    private SceneElement()
    {
    }

    public Guid Id { get; private init; }
    public Guid SceneId { get; private init; }
    public JsonDocument? PdfMetadata { get; private init; }
    public int SequenceOrder { get; private init; }
    public SceneElementType Type { get; private init; }
    public JsonDocument? Content { get; private init; }
    public Guid? CharacterId { get; private init; }
    public Vector? Embedding { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public Scenes.Scene? Scene { get; private init; }
    public Characters.Character? Character { get; private init; }

    public static SceneElement Create(
        Guid sceneId,
        int sequenceOrder,
        SceneElementType type,
        JsonDocument? content = null,
        Guid? characterId = null,
        JsonDocument? pdfMetadata = null)
    {
        return new SceneElement
        {
            Id = Guid.CreateVersion7(),
            SceneId = sceneId,
            PdfMetadata = pdfMetadata,
            SequenceOrder = sequenceOrder,
            Type = type,
            Content = content,
            CharacterId = characterId
        };
    }

    public void SetEmbedding(Vector? embedding)
    {
        Embedding = embedding;
    }
}
