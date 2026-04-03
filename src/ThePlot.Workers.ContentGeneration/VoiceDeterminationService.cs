using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector;
using ThePlot.Core.Characters;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.Voices;
using ThePlot.Database.Abstractions;
using ThePlot.Infrastructure.Embeddings;

namespace ThePlot.Workers.ContentGeneration;

public sealed class VoiceDeterminationService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IVoiceRepository voiceRepository,
    ICharacterRepository characterRepository,
    ISceneRepository sceneRepository,
    ISceneElementRepository sceneElementRepository,
    IQueryFactory<Scene, ISceneQuery> sceneQueryFactory,
    IQueryFactory<SceneElement, ISceneElementQuery> sceneElementQueryFactory,
    IQueryFactory<Character, ICharacterQuery> characterQueryFactory,
    IQueryFactory<Voice, IVoiceQuery> voiceQueryFactory,
    IChatClient chatClient,
    IEmbeddingClient embeddingClient,
    ILogger<VoiceDeterminationService> logger)
{
    public async Task EnsureVoicesForScreenplayAsync(Guid screenplayId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("VoiceDetermination");

        var context = await BuildScreenplayContextAsync(screenplayId, ct);
        if (string.IsNullOrWhiteSpace(context.Summary))
        {
            logger.LogWarning("Screenplay {ScreenplayId} has no speakable content for voice determination.", screenplayId);
            return;
        }

        var narratorQuery = voiceQueryFactory.Create().ByScreenplayId(screenplayId).ByRole(VoiceRole.Narrator);
        var hasNarrator = await voiceRepository.ExistsByQueryAsync(narratorQuery, ct);

        if (!hasNarrator)
        {
            await ResolveNarratorAsync(screenplayId, context.Summary, ct);
        }

        foreach (var (characterId, characterName, dialogue) in context.Characters)
        {
            var character = await characterRepository.GetByKeyAsync(characterId, ct);
            if (character is null || character.VoiceId != null)
            {
                continue;
            }

            var voice = await ResolveCharacterVoiceAsync(
                screenplayId, characterId, characterName, dialogue, context.Summary, ct);

            character.AssignVoice(voice.Id);
        }

        await uow.CommitAsync(ct);
    }

    public async Task<Guid> GetNarratorVoiceIdAsync(Guid screenplayId, CancellationToken ct)
    {
        var query = voiceQueryFactory.Create().ByScreenplayId(screenplayId).ByRole(VoiceRole.Narrator);
        var voice = await voiceRepository.GetFirstByQueryAsync(query, ct)
                    ?? throw new InvalidOperationException(
                        $"No narrator voice found for screenplay {screenplayId}.");
        return voice.Id;
    }

    public async Task<Dictionary<Guid, Guid>> GetCharacterVoiceMapAsync(Guid screenplayId, CancellationToken ct)
    {
        var voiceIds = await voiceRepository.GetByQueryAsync(
            voiceQueryFactory.Create().ByScreenplayId(screenplayId),
            v => v.Id,
            ct);

        if (voiceIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var characters = await characterRepository.GetByQueryAsync(
            characterQueryFactory.Create().ByVoiceIds(voiceIds), ct);

        return characters
            .Where(c => c.VoiceId.HasValue)
            .ToDictionary(c => c.Id, c => c.VoiceId!.Value);
    }

    private async Task ResolveNarratorAsync(Guid screenplayId, string screenplaySummary, CancellationToken ct)
    {
        var description = await GenerateVoiceDescriptionAsync(
            $"""
            Read the screenplay below carefully. Based on its tone, setting, genre, and subject matter, describe the ideal narrator as a person in 1-2 sentences.
            Focus on vocal qualities (pitch, texture, pacing, accent) and emotional delivery that match THIS specific story.
            Write a direct description starting with "A" or "An" — do not list attributes or write a casting brief.
            Respond ONLY with the description.

            Screenplay:
            {screenplaySummary}
            """,
            ct);

        var embedding = await embeddingClient.GetEmbeddingAsync(description, ct: ct);
        var vector = new Vector(embedding);

        var narrator = Voice.CreateNarrator(screenplayId, description);
        narrator.SetEmbedding(vector);
        await voiceRepository.AddAsync(narrator, ct);
    }

    private async Task<Voice> ResolveCharacterVoiceAsync(
        Guid screenplayId,
        Guid characterId,
        string characterName,
        string dialogue,
        string screenplaySummary,
        CancellationToken ct)
    {
        var description = await GenerateVoiceDescriptionAsync(
            $"""
            Read the dialogue and screenplay context below. Describe the voice of the character "{characterName}" as a person in 1-2 sentences.
            Infer their age, vocal texture, accent, pacing, and emotional tone from what they say and how they fit into the story.
            Write a direct description starting with "A" or "An" — do not list attributes or write a casting brief.
            Respond ONLY with the description.

            Character dialogue:
            {dialogue}

            Screenplay context:
            {screenplaySummary}
            """,
            ct);

        var embedding = await embeddingClient.GetEmbeddingAsync(description, ct: ct);
        var vector = new Vector(embedding);

        var voice = Voice.CreateForCharacter(screenplayId, characterId, characterName, description);
        voice.SetEmbedding(vector);
        await voiceRepository.AddAsync(voice, ct);
        return voice;
    }

    private async Task<string> GenerateVoiceDescriptionAsync(string prompt, CancellationToken ct)
    {
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
        return response.Text?.Trim() ?? "";
    }

    private async Task<ScreenplayContext> BuildScreenplayContextAsync(Guid screenplayId, CancellationToken ct)
    {
        var sceneQuery = sceneQueryFactory.Create()
            .ByScreenplayId(screenplayId)
            .OrderByDateCreated();
        var sceneIds = await sceneRepository.GetByQueryAsync(sceneQuery, s => s.Id, ct);

        var elementQuery = sceneElementQueryFactory.Create()
            .BySceneIds(sceneIds)
            .OrderBySequenceOrder();
        var elements = await sceneElementRepository.GetByQueryAsync(elementQuery, ct);

        var characterIds = elements
            .Where(e => e.CharacterId.HasValue)
            .Select(e => e.CharacterId!.Value)
            .Distinct()
            .ToList();

        var characters = characterIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await characterRepository.GetByQueryAsync(
                    characterQueryFactory.Create().ByIds(characterIds), ct))
                .ToDictionary(c => c.Id, c => c.Name);

        var summarySb = new StringBuilder(4096);
        var characterDialogue = new Dictionary<Guid, StringBuilder>();

        foreach (var el in elements)
        {
            var text = SceneElementText.Extract(el.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (el.Type is SceneElementType.Action or SceneElementType.Transition or SceneElementType.VoiceOver)
            {
                summarySb.AppendLine($"[{el.Type}] {text}");
            }
            else if (el.Type is SceneElementType.Dialogue or SceneElementType.Parenthetical)
            {
                var charName = el.CharacterId.HasValue && characters.TryGetValue(el.CharacterId.Value, out var n) ? n : "UNKNOWN";
                summarySb.AppendLine($"{charName}: {text}");

                if (el.CharacterId.HasValue)
                {
                    if (!characterDialogue.TryGetValue(el.CharacterId.Value, out var sb))
                    {
                        sb = new StringBuilder(1024);
                        characterDialogue[el.CharacterId.Value] = sb;
                    }

                    sb.AppendLine(text);
                }
            }

            if (summarySb.Length > 12_000)
            {
                break;
            }
        }

        var charList = characterDialogue
            .Where(kv => characters.ContainsKey(kv.Key))
            .Select(kv => (
                CharacterId: kv.Key,
                Name: characters[kv.Key],
                Dialogue: kv.Value.ToString().Trim()))
            .ToList();

        return new ScreenplayContext(summarySb.ToString().Trim(), charList);
    }

    private sealed record ScreenplayContext(
        string Summary,
        List<(Guid CharacterId, string Name, string Dialogue)> Characters);
}
