using System.Text.RegularExpressions;

namespace ThePlot.Workers.PdfProcessing.Parsing;

/// <summary>
/// Analyzes spatial layout of PDF lines to classify screenplay elements
/// based on indentation thresholds derived from the document itself.
/// </summary>
public sealed partial class LayoutAnalyzer(ScreenplayConfig config)
{
    public IndentationThresholds DetectThresholds(IEnumerable<RawLine> rawLines)
    {
        var histogram = new Dictionary<int, int>();

        foreach (var line in rawLines)
        {
            if (line.PageNum == 1 || line.Bbox.X0 >= config.XTransition)
                continue;

            var rounded = (int)MathF.Round(line.Bbox.X0);
            histogram[rounded] = histogram.GetValueOrDefault(rounded) + 1;
        }

        if (histogram.Count == 0)
        {
            return BuildThresholds(
                config.DefaultActionX, config.DefaultDialogueX,
                config.DefaultParenX, config.DefaultCharX);
        }

        var clusters = ClusterHistogram(histogram);

        float ax, dx, px, cx;
        if (clusters.Count >= 4)
        {
            (ax, dx, px, cx) = MatchClustersToRoles(clusters);
        }
        else if (clusters.Count == 3)
        {
            ax = clusters[0];
            dx = clusters[1];
            cx = clusters[2];
            px = (dx + cx) / 2;
        }
        else
        {
            ax = config.DefaultActionX;
            dx = config.DefaultDialogueX;
            px = config.DefaultParenX;
            cx = config.DefaultCharX;
        }

        return BuildThresholds(ax, dx, px, cx);
    }

    public object ClassifyLine(RawLine line, IndentationThresholds th)
    {
        if (line.PageNum == 1)
            return ElementType.SectionHeader;

        var x0 = line.Bbox.X0;

        if (x0 >= config.XPageNumber)
            return InternalType.PageNumber;
        if (x0 >= config.XTransition)
            return ElementType.Transition;

        if (config.SceneHeadingPattern.IsMatch(line.Text))
            return ElementType.SceneHeading;

        if (x0 >= th.CharacterMin && x0 <= th.CharacterMax)
        {
            var upper = line.Text.ToUpperInvariant();
            if (config.SectionKeywords.Contains(upper))
                return ElementType.SectionHeader;
            if (config.ContinuationMarkers.Contains(upper))
                return InternalType.PageNumber;
            return InternalType.Character;
        }

        if (x0 >= th.ParenMin && x0 <= th.ParenMax)
            return ElementType.Parenthetical;
        if (x0 >= th.DialogueMin && x0 <= th.DialogueMax)
            return ElementType.Dialogue;

        if (IsAllCapsSlug(line.Text))
            return ElementType.SceneHeading;
        if (IsSceneNumber(line.Text))
            return InternalType.PageNumber;

        return ElementType.Action;
    }

    private (float ax, float dx, float px, float cx) MatchClustersToRoles(List<float> clusters)
    {
        float[] defaults =
        [
            config.DefaultActionX,
            config.DefaultDialogueX,
            config.DefaultParenX,
            config.DefaultCharX
        ];

        var available = new List<float>(clusters);
        var assigned = new float[4];

        for (var i = 0; i < defaults.Length; i++)
        {
            var target = defaults[i];
            var bestIdx = 0;
            var bestDist = MathF.Abs(available[0] - target);

            for (var j = 1; j < available.Count; j++)
            {
                var dist = MathF.Abs(available[j] - target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }

            assigned[i] = available[bestIdx];
            available.RemoveAt(bestIdx);
        }

        return (assigned[0], assigned[1], assigned[2], assigned[3]);
    }

    private static List<float> ClusterHistogram(Dictionary<int, int> histogram)
    {
        var clusters = new List<(float center, int count)>();
        var sortedKeys = histogram.Keys.Order().ToList();

        foreach (var x in sortedKeys)
        {
            if (clusters.Count > 0 && x - clusters[^1].center < 15)
            {
                var (cx, cc) = clusters[^1];
                var total = cc + histogram[x];
                clusters[^1] = ((cx * cc + x * histogram[x]) / total, total);
            }
            else
            {
                clusters.Add((x, histogram[x]));
            }
        }

        var peak = clusters.Max(c => c.count);
        var threshold = peak * 0.01f;

        return clusters
            .Where(c => c.count > threshold)
            .Select(c => c.center)
            .Order()
            .ToList();
    }

    private IndentationThresholds BuildThresholds(float ax, float dx, float px, float cx)
    {
        return new IndentationThresholds(
            ActionMax: (ax + dx) / 2,
            DialogueMin: (ax + dx) / 2,
            DialogueMax: (dx + px) / 2,
            ParenMin: (dx + px) / 2,
            ParenMax: (px + cx) / 2,
            CharacterMin: (px + cx) / 2,
            CharacterMax: config.XTransition);
    }

    private static bool IsSceneNumber(string text) =>
        SceneNumberRegex().IsMatch(text.AsSpan().Trim());

    private static bool IsAllCapsSlug(string text)
    {
        var letters = LettersOnlyRegex().Replace(text, "");
        if (letters.Length < 4 || letters != letters.ToUpperInvariant())
            return false;

        var words = NonWordRegex().Replace(text, " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
            return false;

        return !AllCapsSlugRejectRegex().IsMatch(text);
    }

    [GeneratedRegex(@"^\d+[A-Z]?\.?$")]
    private static partial Regex SceneNumberRegex();

    [GeneratedRegex(@"[^A-Za-z]")]
    private static partial Regex LettersOnlyRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"[(\[\]]|--\s*$|\.{3}")]
    private static partial Regex AllCapsSlugRejectRegex();
}
