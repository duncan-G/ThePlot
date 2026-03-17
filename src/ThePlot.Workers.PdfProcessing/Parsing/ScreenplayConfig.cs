using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace ThePlot.Workers.PdfProcessing.Parsing;

public sealed partial class ScreenplayConfig
{
    public float DefaultActionX { get; init; } = 108.0f;
    public float DefaultDialogueX { get; init; } = 180.0f;
    public float DefaultParenX { get; init; } = 209.0f;
    public float DefaultCharX { get; init; } = 252.0f;

    public float XPageNumber { get; init; } = 490.0f;
    public float XTransition { get; init; } = 430.0f;

    public FrozenSet<string> TimeOfDayKeywords { get; } = FrozenSet.ToFrozenSet([
        "DAY", "NIGHT", "DAWN", "DUSK", "SUNSET", "SUNRISE",
        "EVENING", "MORNING", "AFTERNOON", "LATER", "CONTINUOUS",
        "MOMENTS LATER", "SAME TIME", "MAGIC HOUR",
    ]);

    public FrozenSet<string> TransitionKeywords { get; } = FrozenSet.ToFrozenSet([
        "CUT TO", "FADE OUT", "FADE IN", "DISSOLVE TO",
        "SMASH CUT", "SLAM CUT", "HARD CUT",
    ]);

    public FrozenSet<string> SectionKeywords { get; } = FrozenSet.ToFrozenSet([
        "PROLOGUE", "EPILOGUE", "THE END", "END CREDITS", "TITLE CARD",
    ]);

    public FrozenSet<string> ContinuationMarkers { get; } = FrozenSet.ToFrozenSet([
        "(MORE)", "(CONTINUED)", "CONTINUED:",
    ]);

    public FrozenSet<string> CreditKeywords { get; } = FrozenSet.ToFrozenSet([
        "written by", "screenplay by", "story by", "teleplay by",
        "screen story by", "based on", "created by",
    ]);

    public Regex SceneHeadingPattern { get; } = SceneHeadingRegex();

    public Dictionary<string, string> LocationTypeMap { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INT."] = "INT",
        ["EXT."] = "EXT",
        ["INT./EXT."] = "I/E",
        ["INT/EXT."] = "I/E",
        ["I/E."] = "I/E",
    };

    [GeneratedRegex(@"^(INT\.|EXT\.|INT\./EXT\.?|I/E\.?|INT/EXT\.?)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex SceneHeadingRegex();
}
