using System.Text.Json;
using Pgvector;
using ThePlot.Core.Locations;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Scenes;

public sealed class Scene : IDateStamped
{
    private Scene()
    {
    }

    public Guid Id { get; private init; }
    public Guid ScreenplayId { get; private init; }
    public JsonDocument? PdfMetadata { get; private init; }
    public string Heading { get; private init; } = null!;
    public LocationType InteriorExterior { get; private init; }
    public Guid? LocationId { get; private init; }
    public string? TimeOfDay { get; private init; }
    public Vector? Embedding { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public Screenplays.Screenplay? Screenplay { get; private init; }
    public Location? Location { get; private init; }
    public IReadOnlyList<SceneElements.SceneElement>? SceneElements { get; private init; }

    public static Scene Create(
        Guid screenplayId,
        string heading,
        LocationType interiorExterior,
        Guid? locationId = null,
        string? timeOfDay = null,
        JsonDocument? pdfMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);

        return new Scene
        {
            Id = Guid.CreateVersion7(),
            ScreenplayId = screenplayId,
            PdfMetadata = pdfMetadata,
            Heading = heading.Trim(),
            InteriorExterior = interiorExterior,
            LocationId = locationId,
            TimeOfDay = timeOfDay?.Trim()
        };
    }

    public void SetEmbedding(Vector? embedding)
    {
        Embedding = embedding;
    }
}
