namespace ThePlot.Workers.PdfProcessing.Parsing;

public readonly record struct BoundingBox(float X0, float Y0, float X1, float Y1)
{
    public BoundingBox Merge(BoundingBox other) => new(
        Math.Min(X0, other.X0),
        Math.Min(Y0, other.Y0),
        Math.Max(X1, other.X1),
        Math.Max(Y1, other.Y1));
}

public readonly record struct RawLine(int PageNum, string Text, BoundingBox Bbox);

/// <summary>Screenplay element types used in final output.</summary>
public enum ElementType
{
    SceneHeading,
    Action,
    Dialogue,
    VoiceOver,
    Parenthetical,
    Transition,
    SectionHeader
}

/// <summary>Types used only during classification, never in final output.</summary>
public enum InternalType
{
    Character,
    PageNumber
}

public readonly record struct IndentationThresholds(
    float ActionMax,
    float DialogueMin,
    float DialogueMax,
    float ParenMin,
    float ParenMax,
    float CharacterMin,
    float CharacterMax);

public sealed class ParsedElement
{
    public required ElementType Type { get; set; }
    public required string Text { get; set; }
    public required int Page { get; set; }
    public required BoundingBox Bbox { get; set; }
    public string? Character { get; set; }
}

public sealed class ParsedScene
{
    public required string Heading { get; set; }
    public required int Page { get; set; }
    public string? LocationType { get; set; }
    public string Location { get; set; } = "";
    public string TimeOfDay { get; set; } = "";
    public bool IsContinuation { get; set; }
    public List<ParsedElement> Elements { get; } = [];
}

public sealed class ParsedScreenplay
{
    public string Title { get; set; } = "";
    public List<string> Authors { get; } = [];
    public List<ParsedScene> Scenes { get; } = [];
}
