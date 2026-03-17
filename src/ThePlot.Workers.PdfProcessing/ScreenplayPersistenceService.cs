using System.Text.Json;
using ThePlot.Core.Characters;
using ThePlot.Core.Locations;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.Screenplays;
using ThePlot.Workers.PdfProcessing.Parsing;
using ThePlot.Database.Abstractions;
using LocationType = ThePlot.Core.Locations.LocationType;

namespace ThePlot.Workers.PdfProcessing;

/// <summary>
/// Maps a parsed chunk's data to Core domain entities and persists them.
/// Screenplay is created by PdfValidation with placeholder values; this service updates title and authors only when processing page 1.
/// </summary>
public sealed class ScreenplayPersistenceService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IQueryFactory<Screenplay, IScreenplayQuery> screenplayQueryFactory,
    IScreenplayRepository screenplayRepository,
    ISceneRepository sceneRepository,
    ISceneElementRepository sceneElementRepository,
    ICharacterRepository characterRepository,
    ILocationRepository locationRepository,
    ILogger<ScreenplayPersistenceService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveChunkAsync(
        Guid screenplayId,
        string sourceBlobName,
        int startPage,
        ParsedScreenplay parsed,
        CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("SaveChunk");

        if (startPage == 1 && (parsed.Title.Length > 0 || parsed.Authors.Count > 0))
        {
            var placeholderTitle = Path.GetFileNameWithoutExtension(sourceBlobName);
            var query = screenplayQueryFactory.Create().ById(screenplayId);
            await screenplayRepository.UpdateByQueryAsync(
                query,
                set => set.SetProperty(s => s.Title, parsed.Title.Length > 0 ? parsed.Title : placeholderTitle)
                    .SetProperty(s => s.Authors, parsed.Authors.Count > 0 ? parsed.Authors.ToArray() : []),
                ct);
        }

        var characterCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var locationCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsedScene in parsed.Scenes)
        {
            await EnsureCharactersAsync(parsedScene.Elements, characterCache, ct);
            var locationId = await EnsureLocationAsync(parsedScene.Location, locationCache, ct);

            var scene = Scene.Create(
                screenplayId: screenplayId,
                heading: parsedScene.Heading,
                interiorExterior: MapLocationType(parsedScene.LocationType),
                locationId: locationId,
                timeOfDay: parsedScene.TimeOfDay.Length > 0 ? parsedScene.TimeOfDay : null,
                pdfMetadata: SerializePdfMetadata(parsedScene.Page));
            await sceneRepository.AddAsync(scene, ct);

            var sequenceOrder = 0;
            foreach (var parsedElement in parsedScene.Elements)
            {
                var elementType = MapElementType(parsedElement.Type);
                if (elementType is null)
                    continue;

                Guid? characterId = parsedElement.Character is not null
                    && characterCache.TryGetValue(NormalizeCharacterName(parsedElement.Character), out var charId)
                    ? charId
                    : null;

                var sceneElement = SceneElement.Create(
                    sceneId: scene.Id,
                    sequenceOrder: sequenceOrder++,
                    type: elementType.Value,
                    content: SerializeContent(parsedElement.Text),
                    characterId: characterId,
                    pdfMetadata: SerializeElementPdfMetadata(parsedElement));
                await sceneElementRepository.AddAsync(sceneElement, ct);
            }
        }

        await uow.SaveChangesAsync(ct);
        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Saved chunk for screenplay {ScreenplayId} with {SceneCount} scenes",
            screenplayId, parsed.Scenes.Count);
    }

    private static string NormalizeCharacterName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim();

    private async Task EnsureCharactersAsync(
        List<ParsedElement> elements,
        Dictionary<string, Guid> cache,
        CancellationToken ct)
    {
        var names = elements
            .Where(e => e.Character is not null)
            .Select(e => NormalizeCharacterName(e.Character))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (cache.ContainsKey(name))
                continue;

            var character = Character.Create(name);
            await characterRepository.AddAsync(character, ct);
            cache[name] = character.Id;
        }
    }

    private async Task<Guid?> EnsureLocationAsync(
        string locationDesc,
        Dictionary<string, Guid> cache,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locationDesc))
            return null;

        if (cache.TryGetValue(locationDesc, out var existingId))
            return existingId;

        var location = Location.Create(locationDesc);
        await locationRepository.AddAsync(location, ct);
        cache[locationDesc] = location.Id;
        return location.Id;
    }

    private static LocationType MapLocationType(string? type) => type switch
    {
        "INT" => LocationType.Int,
        "EXT" => LocationType.Ext,
        "I/E" => LocationType.IE,
        _ => LocationType.Int
    };

    private static SceneElementType? MapElementType(ElementType type) => type switch
    {
        ElementType.Action => SceneElementType.Action,
        ElementType.Dialogue => SceneElementType.Dialogue,
        ElementType.VoiceOver => SceneElementType.VoiceOver,
        ElementType.Parenthetical => SceneElementType.Parenthetical,
        ElementType.Transition => SceneElementType.Transition,
        _ => null
    };

    private static JsonDocument SerializePdfMetadata(int page)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(
            new { page }, JsonOptions));
    }

    private static JsonDocument SerializeElementPdfMetadata(ParsedElement element)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            page = element.Page,
            bbox = new
            {
                x0 = element.Bbox.X0,
                y0 = element.Bbox.Y0,
                x1 = element.Bbox.X1,
                y1 = element.Bbox.Y1
            }
        }, JsonOptions));
    }

    private static JsonDocument SerializeContent(string text)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(
            new { text }, JsonOptions));
    }
}
