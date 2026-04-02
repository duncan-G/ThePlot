using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Voices;
using ThePlot.Infrastructure;

namespace ThePlot.Workers.ContentGeneration;

public sealed class GenerationGraphBuilder(ThePlotContext db, PreGenerationAnalysisService _analysisService)
{
    public async Task BuildGraphAsync(GenerationRun run, CancellationToken ct)
    {
        if (await db.GenerationNodes.AnyAsync(n => n.GenerationRunId == run.Id, ct))
        {
            return;
        }

        var screenplayId = run.ScreenplayId;
        var scenes = await db.Scenes
            .AsNoTracking()
            .Where(s => s.ScreenplayId == screenplayId)
            .OrderBy(s => s.DateCreated)
            .Select(s => new { s.Id })
            .ToListAsync(ct);

        Guid narratorId;
        Dictionary<Guid, Guid> characterVoices;
        try
        {
            narratorId = await db.Voices.AsNoTracking()
                .Where(v => v.ScreenplayId == screenplayId && v.Role == VoiceRole.Narrator)
                .Select(v => v.Id)
                .FirstAsync(ct);

            characterVoices = await db.Characters.AsNoTracking()
                .Where(c => c.VoiceId != null)
                .Join(
                    db.Voices.AsNoTracking().Where(v => v.ScreenplayId == screenplayId),
                    c => c.VoiceId,
                    v => v.Id,
                    (c, v) => new { c.Id, VoiceId = v.Id })
                .ToDictionaryAsync(x => x.Id, x => x.VoiceId, ct);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Voices are not prepared for this screenplay; complete voice determination first.");
        }

        var contentNodes = new List<GenerationNode>();
        var scenesWithElements = new List<(Guid SceneId, IReadOnlyList<SceneElement> Elements)>();

        foreach (var scene in scenes)
        {
            var elements = await db.SceneElements.AsNoTracking()
                .Where(e => e.SceneId == scene.Id)
                .OrderBy(e => e.SequenceOrder)
                .ToListAsync(ct);

            scenesWithElements.Add((scene.Id, elements));

            var ordered = elements;
            List<DialogueBatchSegment>? batch = null;

            void FlushBatch()
            {
                if (batch is not { Count: > 0 })
                {
                    return;
                }

                var payload = JsonSerializer.SerializeToDocument(
                    SceneElementBatching.LinesPayload(scene.Id, batch, characterVoices, narratorId));

                contentNodes.Add(GenerationNode.Create(
                    run.Id,
                    GenerationNodeKind.VoicePromptBatch,
                    GenerationNodeStatus.Ready,
                    payload));
                batch = null;
            }

            foreach (var el in ordered)
            {
                if (SceneElementText.IsDialogueBatchMember(el.Type))
                {
                    var text = SceneElementText.Extract(el.Content) ?? "";
                    batch ??= [];
                    batch.Add(new DialogueBatchSegment(el.Id, el.Type, el.CharacterId, text));
                    continue;
                }

                FlushBatch();

                if (el.Type is SceneElementType.Action or SceneElementType.VoiceOver or SceneElementType.Transition)
                {
                    var text = SceneElementText.Extract(el.Content) ?? "";
                    var kind = el.Type == SceneElementType.VoiceOver
                        ? GenerationNodeKind.VoiceOverElement
                        : GenerationNodeKind.ActionElement;

                    var payload = JsonSerializer.SerializeToDocument(
                        SceneElementBatching.SingleElementPayload(scene.Id, el.Id, kind, narratorId, text));

                    contentNodes.Add(GenerationNode.Create(run.Id, kind, GenerationNodeStatus.Ready, payload));
                }
            }

            FlushBatch();
        }

        var runAnalysis = _analysisService.AnalyzeScreenplayElements(screenplayId, scenesWithElements);
        var root = runAnalysis.RootElement;
        var speakable = root.TryGetProperty("speakable", out var sp) && sp.GetBoolean();
        var blocking = root.TryGetProperty("blockingReasons", out var br) && br.ValueKind == JsonValueKind.Array
            ? string.Join("; ", br.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)))
            : null;

        var analysisNode = GenerationNode.Create(
            run.Id,
            GenerationNodeKind.PreGenerationAnalysis,
            GenerationNodeStatus.Ready);

        analysisNode.SetAnalysisResult(runAnalysis);

        if (!speakable)
        {
            analysisNode.MarkBlocked(blocking ?? "Pre-generation analysis reported speakability issues.");
            db.GenerationNodes.Add(analysisNode);
            await db.SaveChangesAsync(ct);
            return;
        }

        db.GenerationNodes.Add(analysisNode);
        await db.SaveChangesAsync(ct);

        foreach (var n in contentNodes)
        {
            db.GenerationNodes.Add(n);
        }

        await db.SaveChangesAsync(ct);

        foreach (var n in contentNodes)
        {
            db.GenerationEdges.Add(GenerationEdge.Create(analysisNode.Id, n.Id));
        }

        await db.SaveChangesAsync(ct);
    }
}
