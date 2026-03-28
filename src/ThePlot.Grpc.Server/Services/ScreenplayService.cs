using System.Text.Json;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;
using ThePlot.Core.Locations;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ScreenplayEntity = ThePlot.Core.Screenplays.Screenplay;
using ThePlot.Infrastructure;
using ThePlot.Database.Abstractions;

namespace ThePlot.Grpc.Server.Services;

public sealed class ScreenplayGrpcService(
    ILogger<ScreenplayGrpcService> logger,
    ImportStatusEventBus eventBus,
    IServiceScopeFactory scopeFactory) : ScreenplayService.ScreenplayServiceBase
{
    public override async Task StreamImportStatus(
        StreamImportStatusRequest request,
        IServerStreamWriter<ImportStatusEvent> responseStream,
        ServerCallContext context)
    {
        var blobName = request.BlobName;
        if (string.IsNullOrWhiteSpace(blobName))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "blob_name is required"));

        logger.LogInformation("Client subscribed to import status for blob {BlobName}", blobName);

        var reader = eventBus.Subscribe(blobName);
        try
        {
            await foreach (var evt in reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(evt, context.CancellationToken);

                var isTerminal = evt.Kind is "ValidationFailed" or "ImportFailed";
                var isComplete = evt.Kind == "ChunkProcessDone" && evt.TotalPages > 0 && evt.EndPage >= evt.TotalPages;

                if (isTerminal)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Client disconnected from import status for blob {BlobName}", blobName);
        }
        finally
        {
            eventBus.Unsubscribe(blobName, reader);
        }
    }

    public override async Task<ListScreenplaysResponse> ListScreenplays(
        ListScreenplaysRequest request,
        ServerCallContext context)
    {
        var pageSize = request.PageSize is > 0 and <= 50 ? request.PageSize : 20;

        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var uowFactory = sp.GetRequiredService<IUnitOfWorkFactory>();
        var pagingTokenHelper = sp.GetRequiredService<PagingTokenHelper>();

        ScreenplayPagingToken pagingToken;
        if (!string.IsNullOrWhiteSpace(request.PageToken))
        {
            pagingToken = pagingTokenHelper.Decode<ScreenplayPagingToken>(request.PageToken)
                          ?? new ScreenplayPagingToken(null, pageSize);
        }
        else
        {
            pagingToken = new ScreenplayPagingToken(null, pageSize);
        }

        ListScreenplaysResponse response;
        using (uowFactory.CreateReadOnly("ListScreenplays"))
        {
            var queryFactory = sp.GetRequiredService<IQueryFactory<ScreenplayEntity, ThePlot.Core.Screenplays.IScreenplayQuery>>();
            var repo = sp.GetRequiredService<ThePlot.Core.Screenplays.IScreenplayRepository>();

            var page = await repo.GetByQueryPagedAsync(queryFactory.Create(), pagingToken, context.CancellationToken);

            response = new ListScreenplaysResponse
            {
                NextPageToken = page.NextPageToken ?? "",
            };

            foreach (var screenplay in page.Items)
            {
                response.Items.Add(new ScreenplaySummary
                {
                    Id = screenplay.Id.ToString(),
                    Title = screenplay.Title,
                    TotalPages = 0,
                    DateCreated = screenplay.DateCreated.ToString("O"),
                });
                response.Items[^1].Authors.AddRange(screenplay.Authors);
            }
        }

        return response;
    }

    public override async Task<GetScreenplayResponse> GetScreenplay(
        GetScreenplayRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ScreenplayId, out var screenplayId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid screenplay_id"));

        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var uowFactory = sp.GetRequiredService<IUnitOfWorkFactory>();

        ScreenplayEntity screenplay;
        IReadOnlyList<Scene> scenes;
        IReadOnlyList<SceneElement> elements;
        Dictionary<Guid, Character> characters;
        Dictionary<Guid, Location> locations;
        int totalPages = 0;

        using (uowFactory.CreateReadOnly("GetScreenplay"))
        {
            var screenplayRepo = sp.GetRequiredService<ThePlot.Core.Screenplays.IScreenplayRepository>();
            screenplay = await screenplayRepo.GetByKeyAsync(screenplayId, context.CancellationToken)
                ?? throw new RpcException(new Status(StatusCode.NotFound, "Screenplay not found"));

            var importQueryFactory = sp.GetRequiredService<IQueryFactory<ScreenplayImport, IScreenplayImportQuery>>();
            var importRepo = sp.GetRequiredService<IScreenplayImportRepository>();
            var imports = await importRepo.GetByQueryAsync(importQueryFactory.Create().ByScreenplayId(screenplayId), context.CancellationToken);
            totalPages = imports.FirstOrDefault()?.TotalPages ?? 0;

            var sceneQueryFactory = sp.GetRequiredService<IQueryFactory<Scene, ISceneQuery>>();
            var sceneRepo = sp.GetRequiredService<ISceneRepository>();
            var sceneQuery = sceneQueryFactory.Create().ByScreenplayId(screenplayId);
            scenes = await sceneRepo.GetByQueryAsync(sceneQuery, context.CancellationToken);

            var elementQueryFactory = sp.GetRequiredService<IQueryFactory<SceneElement, ISceneElementQuery>>();
            var elementRepo = sp.GetRequiredService<ISceneElementRepository>();
            var sceneIds = scenes.Select(s => s.Id).ToList();
            elements = [];
            if (sceneIds.Count > 0)
            {
                var elementQuery = elementQueryFactory.Create().BySceneIds(sceneIds);
                elements = await elementRepo.GetByQueryAsync(elementQuery, context.CancellationToken);
            }

            var characterIds = elements.Where(e => e.CharacterId.HasValue).Select(e => e.CharacterId!.Value).Distinct().ToList();
            var locationIds = scenes.Where(s => s.LocationId.HasValue).Select(s => s.LocationId!.Value).Distinct().ToList();

            var db = sp.GetRequiredService<ThePlotContext>();
            characters = characterIds.Count > 0
                ? await db.Characters.AsNoTracking().Where(c => characterIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, context.CancellationToken)
                : new Dictionary<Guid, Character>();
            locations = locationIds.Count > 0
                ? await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, context.CancellationToken)
                : new Dictionary<Guid, Location>();
        }

        var elementsByScene = elements
            .GroupBy(e => e.SceneId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.SequenceOrder).ToList());

        var response = new GetScreenplayResponse
        {
            Id = screenplay.Id.ToString(),
            Title = screenplay.Title,
            TotalPages = totalPages,
        };
        response.Authors.AddRange(screenplay.Authors);

        foreach (var scene in scenes.OrderBy(s => GetPage(s.PdfMetadata)).ThenBy(s => s.DateCreated))
        {
            var sm = new SceneMessage
            {
                Id = scene.Id.ToString(),
                Heading = scene.Heading,
                LocationType = scene.InteriorExterior.ToString(),
                Location = scene.LocationId.HasValue && locations.TryGetValue(scene.LocationId.Value, out var loc)
                    ? loc.Description : "",
                TimeOfDay = scene.TimeOfDay ?? "",
                Page = GetPage(scene.PdfMetadata),
            };

            if (elementsByScene.TryGetValue(scene.Id, out var sceneElements))
            {
                var sceneCharacters = sceneElements
                    .Where(e => e.CharacterId.HasValue)
                    .Select(e => e.CharacterId!.Value)
                    .Distinct()
                    .Select(id => characters.TryGetValue(id, out var c) ? c.Name : null)
                    .Where(n => n is not null)
                    .Distinct()
                    .ToList();
                sm.Characters.AddRange(sceneCharacters!);

                foreach (var el in sceneElements)
                {
                    sm.Elements.Add(new SceneElementMessage
                    {
                        Id = el.Id.ToString(),
                        Type = el.Type.ToString(),
                        Text = GetText(el.Content),
                        Character = el.CharacterId.HasValue && characters.TryGetValue(el.CharacterId.Value, out var ch) ? ch.Name : "",
                        Page = GetPage(el.PdfMetadata),
                        SequenceOrder = el.SequenceOrder,
                    });
                }
            }

            response.Scenes.Add(sm);
        }

        return response;
    }

    private static int GetPage(JsonDocument? pdfMetadata)
    {
        if (pdfMetadata is null) return 0;
        if (pdfMetadata.RootElement.TryGetProperty("page", out var pageProp) && pageProp.TryGetInt32(out var page))
            return page;
        if (pdfMetadata.RootElement.TryGetProperty("Page", out pageProp) && pageProp.TryGetInt32(out page))
            return page;
        return 0;
    }

    private static string GetText(JsonDocument? content)
    {
        if (content is null) return "";
        if (content.RootElement.TryGetProperty("text", out var textProp))
            return textProp.GetString() ?? "";
        if (content.RootElement.TryGetProperty("Text", out textProp))
            return textProp.GetString() ?? "";
        return "";
    }
}
