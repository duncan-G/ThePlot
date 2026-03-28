using System.Text.RegularExpressions;

namespace ThePlot.Workers.PdfProcessing.Parsing;

/// <summary>
/// State machine that assembles classified lines into a structured screenplay.
/// Handles character tracking, dialogue grouping, scene creation, and title page extraction.
/// </summary>
public sealed partial class ScreenplayBuilder(ScreenplayConfig config)
{
    private readonly ParsedScreenplay _screenplay = new();
    private ParsedScene? _currentScene;
    private ParsedElement? _pendingElement;
    private string? _currentCharacter;
    private bool _inVoiceOver;
    private readonly List<string> _pageOneLines = [];

    private static readonly Dictionary<string, string> LocationTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INT."] = "INT",
        ["EXT."] = "EXT",
        ["INT./EXT."] = "I/E",
        ["INT/EXT."] = "I/E",
        ["I/E."] = "I/E",
    };

    public void ProcessLine(RawLine line, object classification, Dictionary<string, string> canonMap)
    {
        if (line.PageNum == 1)
        {
            _pageOneLines.Add(line.Text);
            return;
        }

        if (classification is InternalType internalType)
        {
            if (internalType == InternalType.Character)
            {
                FlushPending();
                _inVoiceOver = line.Text.Contains("(V.O.)", StringComparison.OrdinalIgnoreCase);
                var baseName = NormalizeCharacter(line.Text);
                _currentCharacter = canonMap.GetValueOrDefault(baseName, baseName);
            }
            return;
        }

        var etype = (ElementType)classification;

        if (etype == ElementType.Dialogue && _inVoiceOver)
            etype = ElementType.VoiceOver;

        string? activeCharacter = null;
        if (etype is ElementType.Dialogue or ElementType.VoiceOver or ElementType.Parenthetical)
        {
            activeCharacter = _currentCharacter;
        }
        else
        {
            _currentCharacter = null;
            _inVoiceOver = false;
        }

        if (ShouldMergeWithPending(etype, activeCharacter, line.PageNum))
        {
            _pendingElement!.Text += " " + line.Text;
            _pendingElement.Bbox = _pendingElement.Bbox.Merge(line.Bbox);
        }
        else
        {
            FlushPending();
            _pendingElement = new ParsedElement
            {
                Type = etype,
                Text = line.Text,
                Page = line.PageNum,
                Bbox = line.Bbox,
                Character = activeCharacter
            };
        }
    }

    public ParsedScreenplay Finalize()
    {
        FlushPending();
        ParseTitlePage();
        return _screenplay;
    }

    public static string NormalizeCharacter(string raw) =>
        ParentheticalRegex().Replace(raw, "").Trim();

    private bool ShouldMergeWithPending(ElementType etype, string? character, int pageNum) =>
        _pendingElement is not null
        && _pendingElement.Type == etype
        && _pendingElement.Character == character
        && _pendingElement.Page == pageNum;

    private void FlushPending()
    {
        if (_pendingElement is null)
            return;

        if (_pendingElement.Type == ElementType.SceneHeading)
        {
            CreateNewScene(_pendingElement);
        }
        else
        {
            if (_currentScene is null)
                EnsureContinuationScene(_pendingElement.Page);
            _currentScene!.Elements.Add(_pendingElement);
        }

        _pendingElement = null;
    }

    /// <summary>
    /// Creates a synthetic scene for elements that appear before any scene heading
    /// in a chunk. This happens when a chunk starts mid-scene.
    /// Marked as continuation so post-processing can merge it into the predecessor.
    /// </summary>
    private void EnsureContinuationScene(int page)
    {
        if (_currentScene is not null)
            return;

        _currentScene = new ParsedScene
        {
            Heading = "",
            Page = page,
            IsContinuation = true,
        };
        _screenplay.Scenes.Add(_currentScene);
    }

    private void CreateNewScene(ParsedElement element)
    {
        var heading = TrailingSceneNumberRegex().Replace(element.Text, "");
        element.Text = heading;

        var (locationType, location, timeOfDay) = ParseSceneHeading(heading);

        _currentScene = new ParsedScene
        {
            Heading = heading,
            Page = element.Page,
            LocationType = locationType,
            Location = location,
            TimeOfDay = timeOfDay
        };
        _screenplay.Scenes.Add(_currentScene);
    }

    private (string? locationType, string location, string timeOfDay) ParseSceneHeading(string raw)
    {
        var upper = raw.ToUpperInvariant().Trim();
        string? locationType = null;
        string remainder = raw;

        var sortedPrefixes = LocationTypeMap.Keys
            .OrderByDescending(k => k.Length)
            .ToList();

        foreach (var prefix in sortedPrefixes)
        {
            if (upper.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                locationType = LocationTypeMap[prefix];
                remainder = raw[prefix.Length..].Trim();
                break;
            }
        }

        if (locationType is null)
            return (null, raw, "");

        if (!remainder.Contains(" - "))
            return (locationType, remainder, "");

        var segments = remainder.Split(" - ")
            .Select(s => s.Trim())
            .ToList();

        int? timeIdx = null;
        for (var i = 0; i < segments.Count; i++)
        {
            if (config.TimeOfDayKeywords.Contains(segments[i].ToUpperInvariant()))
            {
                timeIdx = i;
                break;
            }
        }

        string location;
        string timeOfDay;

        if (timeIdx.HasValue && timeIdx.Value > 0)
        {
            location = string.Join(" - ", segments.Take(timeIdx.Value));
            timeOfDay = string.Join(" - ", segments.Skip(timeIdx.Value));
        }
        else
        {
            location = string.Join(" - ", segments.SkipLast(1));
            timeOfDay = segments[^1];
        }

        return (locationType, location, timeOfDay);
    }

    private void ParseTitlePage()
    {
        if (_pageOneLines.Count == 0)
            return;

        _screenplay.Title = _pageOneLines[0];

        var authorFragments = new List<string>();
        var collecting = false;

        foreach (var line in _pageOneLines)
        {
            var lower = line.ToLowerInvariant().Trim();
            if (config.CreditKeywords.Any(kw => lower == kw || lower.StartsWith(kw + " ", StringComparison.Ordinal)))
            {
                if (collecting && authorFragments.Count > 0)
                    break;
                collecting = true;
                continue;
            }

            if (!collecting)
                continue;
            if (lower.Length == 0)
                break;
            authorFragments.Add(line.Trim());
        }

        _screenplay.Authors.AddRange(ParseAuthors(string.Join(" ", authorFragments)));
    }

    internal static List<string> ParseAuthors(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0)
            return [];

        s = Regex.Replace(s, @",\s+and\s+", " | ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @",\s+&\s+", " | ");
        s = Regex.Replace(s, @"\s+&\s+", " | ");
        s = Regex.Replace(s, @"\s+and\s+", " | ", RegexOptions.IgnoreCase);

        var parts = new List<string>();
        foreach (var chunk in s.Split('|'))
        {
            foreach (var name in chunk.Split(','))
            {
                var trimmed = name.Trim();
                if (trimmed.Length > 0)
                    parts.Add(trimmed);
            }
        }

        return parts;
    }

    [GeneratedRegex(@"\s*\(.*?\)")]
    private static partial Regex ParentheticalRegex();

    [GeneratedRegex(@"\s+\d+[A-Z]?$")]
    private static partial Regex TrailingSceneNumberRegex();
}
