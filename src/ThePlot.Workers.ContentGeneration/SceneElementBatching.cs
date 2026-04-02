using System.Text.Json;
using System.Text.Json.Nodes;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.SceneElements;

namespace ThePlot.Workers.ContentGeneration;

public sealed record DialogueBatchSegment(
    Guid SceneElementId,
    SceneElementType Type,
    Guid? CharacterId,
    string Text);

public static class SceneElementBatching
{
    /// <summary>
    /// Groups consecutive dialogue/parenthetical elements into batches. Action, voice-over, and transitions end a batch.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<DialogueBatchSegment>> BuildDialogueBatches(
        IReadOnlyList<SceneElement> elementsOrdered,
        Func<JsonDocument?, string?> getText)
    {
        var batches = new List<List<DialogueBatchSegment>>();
        List<DialogueBatchSegment>? current = null;

        foreach (var el in elementsOrdered)
        {
            if (SceneElementText.IsDialogueBatchMember(el.Type))
            {
                var text = getText(el.Content) ?? "";
                current ??= [];
                current.Add(new DialogueBatchSegment(el.Id, el.Type, el.CharacterId, text));
                continue;
            }

            Flush();
        }

        Flush();
        return batches;

        void Flush()
        {
            if (current is { Count: > 0 })
            {
                batches.Add(current);
                current = null;
            }
        }
    }

    public static JsonObject LinesPayload(
        Guid sceneId,
        IReadOnlyList<DialogueBatchSegment> segments,
        IReadOnlyDictionary<Guid, Guid> characterToVoiceId,
        Guid narratorVoiceId)
    {
        var arr = new JsonArray();
        foreach (var seg in segments)
        {
            var voiceId = seg.CharacterId is { } cid && characterToVoiceId.TryGetValue(cid, out var v)
                ? v
                : narratorVoiceId;

            arr.Add(new JsonObject
            {
                ["elementId"] = seg.SceneElementId.ToString(),
                ["characterId"] = seg.CharacterId?.ToString(),
                ["voiceId"] = voiceId.ToString(),
                ["type"] = seg.Type.ToString(),
                ["text"] = seg.Text,
            });
        }

        return new JsonObject
        {
            ["sceneId"] = sceneId.ToString(),
            ["lines"] = arr,
        };
    }

    public static JsonObject SingleElementPayload(
        Guid sceneId,
        Guid elementId,
        GenerationNodeKind kind,
        Guid voiceId,
        string text)
    {
        return new JsonObject
        {
            ["sceneId"] = sceneId.ToString(),
            ["elementId"] = elementId.ToString(),
            ["kind"] = kind.ToString(),
            ["voiceId"] = voiceId.ToString(),
            ["text"] = text,
        };
    }
}
