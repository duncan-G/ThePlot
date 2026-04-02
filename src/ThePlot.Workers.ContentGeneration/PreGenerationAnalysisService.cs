using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.SceneElements;

namespace ThePlot.Workers.ContentGeneration;

public sealed partial class PreGenerationAnalysisService(ILogger<PreGenerationAnalysisService> logger)
{
    [GeneratedRegex(@"\([^)]+\)", RegexOptions.Compiled)]
    private static partial Regex ParentheticalRegex();

    public JsonDocument AnalyzeScreenplayElements(
        Guid screenplayId,
        IReadOnlyList<(Guid SceneId, IReadOnlyList<SceneElement> Elements)> scenesWithElements)
    {
        var items = new JsonArray();
        var blockingReasons = new JsonArray();
        var instructions = new JsonArray();

        foreach (var (sceneId, elements) in scenesWithElements)
        {
            foreach (var el in elements.OrderBy(e => e.SequenceOrder))
            {
                var text = SceneElementText.Extract(el.Content) ?? "";
                var trimmed = text.Trim();
                if (trimmed.Length == 0 &&
                    el.Type is SceneElementType.Dialogue or SceneElementType.Action
                    or SceneElementType.VoiceOver or SceneElementType.Parenthetical)
                {
                    blockingReasons.Add(
                        $"Empty speakable text for element {el.Id} (scene {sceneId}, type {el.Type}).");
                }

                foreach (Match m in ParentheticalRegex().Matches(text))
                {
                    instructions.Add(new JsonObject
                    {
                        ["elementId"] = el.Id.ToString(),
                        ["sceneId"] = sceneId.ToString(),
                        ["raw"] = m.Value,
                        ["styleHint"] = m.Value.Trim().TrimStart('(').TrimEnd(')'),
                    });
                }

                items.Add(new JsonObject
                {
                    ["elementId"] = el.Id.ToString(),
                    ["sceneId"] = sceneId.ToString(),
                    ["type"] = el.Type.ToString(),
                    ["textLength"] = trimmed.Length,
                });
            }
        }

        var speakable = blockingReasons.Count == 0;
        if (!speakable)
        {
            logger.LogWarning("Pre-generation analysis found issues for screenplay {ScreenplayId}", screenplayId);
        }

        var root = new JsonObject
        {
            ["screenplayId"] = screenplayId.ToString(),
            ["speakable"] = speakable,
            ["elementCount"] = items.Count,
            ["items"] = items,
            ["blockingReasons"] = blockingReasons,
            ["parentheticalInstructions"] = instructions,
            ["analyzedAtUtc"] = DateTime.UtcNow.ToString("O"),
        };

        return JsonDocument.Parse(root.ToJsonString());
    }

    public void ApplyAnalysisToNode(GenerationNode node, JsonDocument runAnalysis, bool speakable, string? blockReason)
    {
        node.SetAnalysisResult(runAnalysis);
        if (!speakable && !string.IsNullOrEmpty(blockReason))
        {
            node.MarkBlocked(blockReason);
        }
    }
}
