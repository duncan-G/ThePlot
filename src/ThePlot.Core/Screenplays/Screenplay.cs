using System.Text.Json;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Screenplays;

public sealed class Screenplay : IDateStamped
{
    private Screenplay()
    {
    }

    public Guid Id { get; private init; }
    public string Title { get; private set; } = null!;
    public IReadOnlyList<string> Authors { get; private set; } = [];
    public JsonDocument? PdfMetadata { get; private init; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
    public DateTime? DateDeleted { get; set; }

    public IReadOnlyList<Scenes.Scene>? Scenes { get; private init; }

    public static Screenplay Create(string title, string[]? authors = null, JsonDocument? pdfMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Screenplay
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Authors = authors ?? [],
            PdfMetadata = pdfMetadata
        };
    }

    public void SoftDelete()
    {
        DateDeleted = DateTime.UtcNow;
    }
}
