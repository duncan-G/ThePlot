using System.Text.Json;
using ThePlot.Core.SceneElements;

namespace ThePlot.Workers.ContentGeneration;

public static class SceneElementText
{
    public static string? Extract(JsonDocument? content)
    {
        if (content == null)
        {
            return null;
        }

        var root = content.RootElement;
        if (root.ValueKind == JsonValueKind.String)
        {
            return root.GetString();
        }

        if (root.TryGetProperty("text", out var t))
        {
            return t.GetString();
        }

        if (root.TryGetProperty("Text", out t))
        {
            return t.GetString();
        }

        return null;
    }

    /// <summary>Whether this type is merged into multi-speaker dialogue batches (vs independent audio nodes).</summary>
    public static bool IsDialogueBatchMember(SceneElementType type) =>
        type is SceneElementType.Dialogue or SceneElementType.Parenthetical;
}
