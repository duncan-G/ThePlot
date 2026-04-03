using System.Text.Json;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Database.Abstractions;

namespace ThePlot.Workers.ContentGeneration;

public sealed class GenerationGraphBuilder(
    IUnitOfWorkFactory unitOfWorkFactory,
    IGenerationNodeRepository nodeRepository,
    IGenerationEdgeRepository edgeRepository,
    IQueryFactory<GenerationNode, IGenerationNodeQuery> nodeQueryFactory,
    ISceneRepository sceneRepository,
    ISceneElementRepository sceneElementRepository,
    IQueryFactory<Scene, ISceneQuery> sceneQueryFactory,
    IQueryFactory<SceneElement, ISceneElementQuery> sceneElementQueryFactory,
    VoiceDeterminationService voiceDeterminationService,
    PreGenerationAnalysisService _analysisService)
{
    public async Task BuildGraphAsync(GenerationRun run, CancellationToken ct)
    {
        if (await nodeRepository.ExistsByQueryAsync(nodeQueryFactory.Create().ByRunId(run.Id), ct))
        {
            return;
        }

        var screenplayId = run.ScreenplayId;

        var sceneIds = await sceneRepository.GetByQueryAsync(
            sceneQueryFactory.Create().ByScreenplayId(screenplayId).OrderByDateCreated(),
            s => s.Id,
            ct);

        Guid narratorId;
        Dictionary<Guid, Guid> characterVoices;
        try
        {
            narratorId = await voiceDeterminationService.GetNarratorVoiceIdAsync(screenplayId, ct);
            characterVoices = await voiceDeterminationService.GetCharacterVoiceMapAsync(screenplayId, ct);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Voices are not prepared for this screenplay; complete voice determination first.");
        }

        var contentNodes = new List<GenerationNode>();
        var scenesWithElements = new List<(Guid SceneId, IReadOnlyList<SceneElement> Elements)>();

        foreach (var sceneId in sceneIds)
        {
            var elements = await sceneElementRepository.GetByQueryAsync(
                sceneElementQueryFactory.Create().BySceneIds([sceneId]).OrderBySequenceOrder(),
                ct);

            scenesWithElements.Add((sceneId, elements));

            List<DialogueBatchSegment>? batch = null;

            void FlushBatch()
            {
                if (batch is not { Count: > 0 })
                {
                    return;
                }

                var payload = JsonSerializer.SerializeToDocument(
                    SceneElementBatching.LinesPayload(sceneId, batch, characterVoices, narratorId));

                contentNodes.Add(GenerationNode.Create(
                    run.Id,
                    GenerationNodeKind.VoicePromptBatch,
                    GenerationNodeStatus.Ready,
                    payload));
                batch = null;
            }

            foreach (var el in elements)
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
                        SceneElementBatching.SingleElementPayload(sceneId, el.Id, kind, narratorId, text));

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

        using var uow = unitOfWorkFactory.CreateReadWrite("BuildGraph");

        if (!speakable)
        {
            analysisNode.MarkBlocked(blocking ?? "Pre-generation analysis reported speakability issues.");
            await nodeRepository.AddAsync(analysisNode, ct);
            await uow.CommitAsync(ct);
            return;
        }

        await nodeRepository.AddAsync(analysisNode, ct);
        await uow.SaveChangesAsync(ct);

        foreach (var n in contentNodes)
        {
            await nodeRepository.AddAsync(n, ct);
        }

        await uow.SaveChangesAsync(ct);

        foreach (var n in contentNodes)
        {
            await edgeRepository.AddAsync(GenerationEdge.Create(analysisNode.Id, n.Id), ct);
        }

        await uow.CommitAsync(ct);
    }
}
