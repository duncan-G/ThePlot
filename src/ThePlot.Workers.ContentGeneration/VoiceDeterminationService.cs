using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Voices;
using ThePlot.Infrastructure;
using ThePlot.Infrastructure.ContentGeneration;
using ThePlot.Infrastructure.Embeddings;

namespace ThePlot.Workers.ContentGeneration;

public sealed class VoiceDeterminationService(
    ThePlotContext db,
    IChatClient chatClient,
    IEmbeddingClient embeddingClient,
    IOptions<ContentGenerationOptions> options,
    ILogger<VoiceDeterminationService> logger)
{
    public async Task EnsureVoicesForScreenplayAsync(Guid screenplayId, CancellationToken ct)
    {
        var threshold = options.Value.VoiceSimilarityThreshold;

        var context = await BuildScreenplayContextAsync(screenplayId, ct);
        if (string.IsNullOrWhiteSpace(context.Summary))
        {
            logger.LogWarning("Screenplay {ScreenplayId} has no speakable content for voice determination.", screenplayId);
            return;
        }

        var hasNarrator = await db.Voices
            .AnyAsync(v => v.ScreenplayId == screenplayId && v.Role == VoiceRole.Narrator, ct);

        if (!hasNarrator)
        {
            await ResolveNarratorAsync(screenplayId, context.Summary, threshold, ct);
        }

        foreach (var (characterId, characterName, dialogue) in context.Characters)
        {
            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
            if (character is null || character.VoiceId != null)
            {
                continue;
            }

            var voice = await ResolveCharacterVoiceAsync(
                screenplayId, characterId, characterName, dialogue, context.Summary, threshold, ct);

            character.AssignVoice(voice.Id);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> GetNarratorVoiceIdAsync(Guid screenplayId, CancellationToken ct) =>
        await db.Voices.AsNoTracking()
            .Where(v => v.ScreenplayId == screenplayId && v.Role == VoiceRole.Narrator)
            .Select(v => v.Id)
            .FirstAsync(ct);

    public async Task<Dictionary<Guid, Guid>> GetCharacterVoiceMapAsync(Guid screenplayId, CancellationToken ct) =>
        await db.Characters.AsNoTracking()
            .Where(c => c.VoiceId != null)
            .Join(
                db.Voices.AsNoTracking().Where(v => v.ScreenplayId == screenplayId),
                c => c.VoiceId,
                v => v.Id,
                (c, v) => new { c.Id, VoiceId = v.Id })
            .ToDictionaryAsync(x => x.Id, x => x.VoiceId, ct);

    private async Task ResolveNarratorAsync(
        Guid screenplayId, string screenplaySummary, double threshold, CancellationToken ct)
    {
        var description = await GenerateVoiceDescriptionAsync(
            $"""
            You are a voice casting director. Given the following screenplay content, describe the ideal narrator voice in 1-2 sentences.
            Focus on tone, gender, age, accent, energy level, and stylistic qualities.
            Respond ONLY with the voice description, nothing else.

            Screenplay content:
            {screenplaySummary}
            """,
            ct);

        var embedding = await embeddingClient.GetEmbeddingAsync(description, ct: ct);
        var vector = new Vector(embedding);

        var match = await FindClosestVoiceAsync(VoiceRole.Narrator, vector, threshold, ct);
        if (match is not null)
        {
            logger.LogInformation("Reusing existing narrator voice {VoiceId} for screenplay {ScreenplayId}.",
                match.Id, screenplayId);
            var narrator = Voice.CreateNarrator(screenplayId, match.Description);
            narrator.SetEmbedding(match.Embedding);
            db.Voices.Add(narrator);
            return;
        }

        var narrator2 = Voice.CreateNarrator(screenplayId, description);
        narrator2.SetEmbedding(vector);
        db.Voices.Add(narrator2);
    }

    private async Task<Voice> ResolveCharacterVoiceAsync(
        Guid screenplayId,
        Guid characterId,
        string characterName,
        string dialogue,
        string screenplaySummary,
        double threshold,
        CancellationToken ct)
    {
        var description = await GenerateVoiceDescriptionAsync(
            $"""
            You are a voice casting director. Based on this character's dialogue and their role within the screenplay, describe their voice in 1-2 sentences.
            Focus on gender, age range, accent, tone, energy, and distinguishing vocal qualities.
            Respond ONLY with the voice description, nothing else.

            Character name: {characterName}

            Character dialogue:
            {dialogue}

            Screenplay context:
            {screenplaySummary}
            """,
            ct);

        var embedding = await embeddingClient.GetEmbeddingAsync(description, ct: ct);
        var vector = new Vector(embedding);

        var match = await FindClosestVoiceAsync(VoiceRole.Character, vector, threshold, ct);
        if (match is not null)
        {
            logger.LogInformation(
                "Reusing existing voice {VoiceId} ('{VoiceName}') for character {CharacterName} in screenplay {ScreenplayId}.",
                match.Id, match.Name, characterName, screenplayId);
            var voice = Voice.CreateForCharacter(screenplayId, characterId, characterName, match.Description);
            voice.SetEmbedding(match.Embedding);
            db.Voices.Add(voice);
            return voice;
        }

        var newVoice = Voice.CreateForCharacter(screenplayId, characterId, characterName, description);
        newVoice.SetEmbedding(vector);
        db.Voices.Add(newVoice);
        return newVoice;
    }

    private async Task<Voice?> FindClosestVoiceAsync(
        VoiceRole role, Vector queryVector, double threshold, CancellationToken ct)
    {
        // pgvector cosine distance operator <=> returns values in [0, 2]; lower = more similar.
        // We filter for voices that have an embedding and pick the nearest one.
        var closest = await db.Voices
            .AsNoTracking()
            .Where(v => v.Role == role && v.Embedding != null)
            .OrderBy(v => v.Embedding!.CosineDistance(queryVector))
            .Select(v => new { Voice = v, Distance = v.Embedding!.CosineDistance(queryVector) })
            .FirstOrDefaultAsync(ct);

        if (closest is null || closest.Distance > threshold)
        {
            return null;
        }

        return closest.Voice;
    }

    private async Task<string> GenerateVoiceDescriptionAsync(string prompt, CancellationToken ct)
    {
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
        return response.Text?.Trim() ?? "";
    }

    private async Task<ScreenplayContext> BuildScreenplayContextAsync(Guid screenplayId, CancellationToken ct)
    {
        var scenes = await db.Scenes.AsNoTracking()
            .Where(s => s.ScreenplayId == screenplayId)
            .OrderBy(s => s.DateCreated)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var elements = await db.SceneElements.AsNoTracking()
            .Where(e => scenes.Contains(e.SceneId))
            .OrderBy(e => e.SequenceOrder)
            .ToListAsync(ct);

        var characters = await db.Characters.AsNoTracking()
            .Where(c => elements.Select(e => e.CharacterId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

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
