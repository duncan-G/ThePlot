using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure;

/// <summary>
/// Runs after all chunks are processed. Merges continuation scenes into predecessors,
/// then deduplicates characters and locations that were created independently per chunk.
/// </summary>
internal sealed class ChunkReconciliationService(
    ISceneRepository sceneRepository,
    ISceneElementRepository sceneElementRepository,
    IQueryFactory<Scene, ISceneQuery> sceneQueryFactory,
    IQueryFactory<SceneElement, ISceneElementQuery> sceneElementQueryFactory,
    IUnitOfWorkFactory unitOfWorkFactory,
    ThePlotContext db,
    ILogger<ChunkReconciliationService> logger) : IChunkReconciliationService
{
    public async Task ReconcileAsync(Guid screenplayId, CancellationToken ct)
    {
        await MergeContinuationScenesAsync(screenplayId, ct);
        await DeduplicateCharactersAsync(screenplayId, ct);
        await DeduplicateLocationsAsync(screenplayId, ct);
    }

    private async Task MergeContinuationScenesAsync(Guid screenplayId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("ReconcileScenes");

        var allScenes = await sceneRepository.GetByQueryAsync(
            sceneQueryFactory.Create().ByScreenplayId(screenplayId), ct);

        var ordered = allScenes
            .OrderBy(s => GetPage(s.PdfMetadata))
            .ThenBy(s => s.DateCreated)
            .ToList();

        var merged = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (!IsContinuation(ordered[i].PdfMetadata))
                continue;

            Scene? predecessor = null;
            for (var j = i - 1; j >= 0; j--)
            {
                if (!IsContinuation(ordered[j].PdfMetadata))
                {
                    predecessor = ordered[j];
                    break;
                }
            }

            if (predecessor is null)
                continue;

            var continuation = ordered[i];

            var predecessorElements = await sceneElementRepository.GetByQueryAsync(
                sceneElementQueryFactory.Create().BySceneIds([predecessor.Id]), ct);
            var maxOrder = predecessorElements.Count > 0
                ? predecessorElements.Max(e => e.SequenceOrder)
                : -1;

            var offset = maxOrder + 1;

            var reparented = await sceneElementRepository.UpdateByQueryAsync(
                sceneElementQueryFactory.Create().BySceneIds([continuation.Id]),
                set => set
                    .SetProperty(e => e.SceneId, predecessor.Id)
                    .SetProperty(e => e.SequenceOrder, e => e.SequenceOrder + offset),
                ct);

            await sceneRepository.RemoveAsync(continuation, ct);
            merged++;

            logger.LogInformation(
                "Merged continuation scene {ContinuationId} (page {Page}) into predecessor {PredecessorId}: {Count} elements re-parented",
                continuation.Id, GetPage(continuation.PdfMetadata), predecessor.Id, reparented);
        }

        if (merged > 0)
        {
            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);
            logger.LogInformation(
                "Reconciled {Count} continuation scene(s) for screenplay {ScreenplayId}",
                merged, screenplayId);
        }
    }

    private async Task DeduplicateCharactersAsync(Guid screenplayId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("DeduplicateCharacters");

        var sceneIds = await db.Scenes.AsNoTracking()
            .Where(s => s.ScreenplayId == screenplayId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (sceneIds.Count == 0) return;

        var characterIds = await db.SceneElements.AsNoTracking()
            .Where(e => sceneIds.Contains(e.SceneId) && e.CharacterId != null)
            .Select(e => e.CharacterId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (characterIds.Count == 0) return;

        var characters = await db.Characters.AsNoTracking()
            .Where(c => characterIds.Contains(c.Id))
            .ToListAsync(ct);

        var groups = characters
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0) return;

        var remapped = 0;
        foreach (var group in groups)
        {
            var winner = group.OrderBy(c => c.Id).First();
            var loserIds = group.Where(c => c.Id != winner.Id).Select(c => c.Id).ToList();

            foreach (var loserId in loserIds)
            {
                remapped += await db.SceneElements
                    .Where(e => e.CharacterId == loserId)
                    .ExecuteUpdateAsync(set => set.SetProperty(e => e.CharacterId, winner.Id), ct);
            }

            await db.Characters
                .Where(c => loserIds.Contains(c.Id))
                .ExecuteDeleteAsync(ct);
        }

        await uow.SaveChangesAsync(ct);
        await uow.CommitAsync(ct);
        logger.LogInformation(
            "Deduplicated characters for screenplay {ScreenplayId}: {Groups} groups, {Remapped} elements remapped",
            screenplayId, groups.Count, remapped);
    }

    private async Task DeduplicateLocationsAsync(Guid screenplayId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("DeduplicateLocations");

        var scenesWithLocations = await db.Scenes.AsNoTracking()
            .Where(s => s.ScreenplayId == screenplayId && s.LocationId != null)
            .Select(s => new { s.Id, LocationId = s.LocationId!.Value })
            .ToListAsync(ct);

        if (scenesWithLocations.Count == 0) return;

        var locationIds = scenesWithLocations.Select(s => s.LocationId).Distinct().ToList();

        var locations = await db.Locations.AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .ToListAsync(ct);

        var groups = locations
            .GroupBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0) return;

        var remapped = 0;
        foreach (var group in groups)
        {
            var winner = group.OrderBy(l => l.Id).First();
            var loserIds = group.Where(l => l.Id != winner.Id).Select(l => l.Id).ToList();

            foreach (var loserId in loserIds)
            {
                remapped += await db.Scenes
                    .Where(s => s.LocationId == loserId)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.LocationId, winner.Id), ct);
            }

            await db.Locations
                .Where(l => loserIds.Contains(l.Id))
                .ExecuteDeleteAsync(ct);
        }

        await uow.SaveChangesAsync(ct);
        await uow.CommitAsync(ct);
        logger.LogInformation(
            "Deduplicated locations for screenplay {ScreenplayId}: {Groups} groups, {Remapped} scenes remapped",
            screenplayId, groups.Count, remapped);
    }

    private static int GetPage(JsonDocument? pdfMetadata)
    {
        if (pdfMetadata is null) return 0;
        if (pdfMetadata.RootElement.TryGetProperty("page", out var prop) && prop.TryGetInt32(out var page))
            return page;
        return 0;
    }

    private static bool IsContinuation(JsonDocument? pdfMetadata)
    {
        if (pdfMetadata is null) return false;
        return pdfMetadata.RootElement.TryGetProperty("continuation", out var prop)
               && prop.ValueKind == JsonValueKind.True;
    }
}
