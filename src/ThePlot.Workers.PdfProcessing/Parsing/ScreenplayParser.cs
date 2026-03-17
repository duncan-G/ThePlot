using System.Text.RegularExpressions;

namespace ThePlot.Workers.PdfProcessing.Parsing;

/// <summary>
/// Orchestrator that wires PDF extraction, layout analysis, and document assembly
/// into a single parse operation. Processes a chunk of pages, not necessarily the
/// entire screenplay.
/// </summary>
public static class ScreenplayParser
{
    private static readonly Regex MultiSpaceRegex = new(@"  +", RegexOptions.Compiled);

    /// <param name="pdfBytes">Raw PDF bytes for the chunk.</param>
    /// <param name="startPage">Actual page number of the first page in the chunk
    /// (used to offset MuPDF's renumbered pages back to their original position).</param>
    public static ParsedScreenplay Parse(byte[] pdfBytes, int startPage = 1)
    {
        var config = new ScreenplayConfig();
        var analyzer = new LayoutAnalyzer(config);
        var builder = new ScreenplayBuilder(config);

        var rawLines = PdfTextExtractor.ExtractLines(pdfBytes, startPage);

        if (rawLines.Count == 0)
        {
            throw new ScreenplayParseException(
                "PDF chunk has no extractable text. It may be empty, scanned (image-only), or corrupted.");
        }

        var thresholds = analyzer.DetectThresholds(rawLines);

        var classifiedData = new List<(RawLine line, object classification)>();
        var rawCharacters = new List<string>();

        foreach (var line in rawLines)
        {
            var cleanText = MultiSpaceRegex.Replace(line.Text, " ");
            var cleanLine = new RawLine(line.PageNum, cleanText, line.Bbox);
            var classification = analyzer.ClassifyLine(cleanLine, thresholds);

            if (classification is InternalType.PageNumber)
                continue;

            if (classification is InternalType.Character)
                rawCharacters.Add(ScreenplayBuilder.NormalizeCharacter(cleanText));

            classifiedData.Add((cleanLine, classification));
        }

        var canonMap = BuildCharacterCanonMap(rawCharacters);

        foreach (var (line, classification) in classifiedData)
            builder.ProcessLine(line, classification, canonMap);

        return builder.Finalize();
    }

    /// <summary>
    /// Groups raw character names that become identical when spaces are removed,
    /// picks the most frequent variant as canonical. Corrects OCR artifacts
    /// like "SA MMIE" → "SAMMIE".
    /// </summary>
    internal static Dictionary<string, string> BuildCharacterCanonMap(List<string> rawNames)
    {
        var freq = new Dictionary<string, int>();
        foreach (var name in rawNames)
            freq[name] = freq.GetValueOrDefault(name) + 1;

        var groups = new Dictionary<string, List<string>>();
        foreach (var name in freq.Keys)
        {
            var key = name.Replace(" ", "");
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(name);
        }

        var canon = new Dictionary<string, string>();
        foreach (var (key, variants) in groups)
        {
            var best = variants.MaxBy(v => freq[v])!;

            if (variants.Count == 1 && best.Contains(' ') && freq[best] <= 2)
            {
                var parts = best.Split(' ');
                if (parts.Length == 2
                    && parts.All(p => p.All(char.IsLetter))
                    && Math.Min(parts[0].Length, parts[1].Length) <= 2)
                {
                    best = key;
                }
            }

            foreach (var v in variants)
                canon[v] = best;
        }

        return canon;
    }
}

public sealed class ScreenplayParseException(string message) : Exception(message);
