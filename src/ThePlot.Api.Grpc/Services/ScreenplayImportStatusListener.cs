using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Database.Abstractions;

namespace ThePlot.Api.Grpc.Services;

/// <summary>
/// Listens to screenplay-import-status queue, persists status to DB,
/// and pushes real-time events to connected streaming clients via <see cref="ImportStatusEventBus"/>.
/// </summary>
public sealed class ScreenplayImportStatusListener(
    ILogger<ScreenplayImportStatusListener> logger,
    ServiceBusClient serviceBusClient,
    IServiceScopeFactory scopeFactory,
    ImportStatusEventBus eventBus) : BackgroundService
{
    public const string QueueName = "screenplay-import-status";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ScreenplayImportStatusListener starting. Queue: {Queue}", QueueName);

        await using var processor = serviceBusClient.CreateProcessor(QueueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var message = args.Message.Body.ToObjectFromJson<ScreenplayImportStatusMessage>(JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize status message");

            await using var scope = scopeFactory.CreateAsyncScope();
            await ProcessStatusMessageAsync(scope.ServiceProvider, message, args.CancellationToken);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process status message. Dead-lettering.");
            await args.DeadLetterMessageAsync(args.Message, "ProcessingFailed", ex.Message, args.CancellationToken);
        }
    }

    private Task HandleProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, Entity: {Entity}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private async Task ProcessStatusMessageAsync(IServiceProvider services, ScreenplayImportStatusMessage msg, CancellationToken ct)
    {
        var importRepo = services.GetRequiredService<IScreenplayImportRepository>();
        var importQueryFactory = services.GetRequiredService<IQueryFactory<ScreenplayImport, IScreenplayImportQuery>>();
        var chunkRepo = services.GetRequiredService<IScreenplayImportChunkRepository>();
        var chunkQueryFactory = services.GetRequiredService<IQueryFactory<ScreenplayImportChunk, IScreenplayImportChunkQuery>>();
        var uowFactory = services.GetRequiredService<IUnitOfWorkFactory>();

        switch (msg.Kind)
        {
            case "BlobUploaded":
                if (msg.SourceBlobName is { } blobName)
                {
                    using (var uow = uowFactory.CreateReadWrite("BlobUploaded"))
                    {
                        var import = ScreenplayImport.Create(blobName);
                        await importRepo.AddAsync(import, ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Created ScreenplayImport for blob {BlobName}", blobName);
                    PublishEvent(blobName, msg);
                }
                break;

            case "ValidationFailed":
                if (msg.SourceBlobName is { } failBlobName && msg.Reason is { } failReason)
                {
                    using (var uow = uowFactory.CreateReadWrite("ValidationFailed"))
                    {
                        var query = importQueryFactory.Create().BySourceBlobName(failBlobName);
                        await importRepo.UpdateByQueryAsync(query, set => set
                            .SetProperty(i => i.ImportFailedAt, DateTimeOffset.UtcNow)
                            .SetProperty(i => i.ImportErrorMessage, failReason), ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Marked import failed for blob {BlobName}: {Reason}", failBlobName, failReason);
                    PublishEvent(failBlobName, msg);
                }
                break;

            case "ValidationPassed":
                if (msg.ScreenplayId is { } screenplayId && msg.SourceBlobName is { } passBlobName)
                {
                    using (var uow = uowFactory.CreateReadWrite("ValidationPassed"))
                    {
                        var query = importQueryFactory.Create().BySourceBlobName(passBlobName);
                        await importRepo.UpdateByQueryAsync(query, set => set
                            .SetProperty(i => i.ScreenplayId, screenplayId)
                            .SetProperty(i => i.ValidatedAt, DateTimeOffset.UtcNow), ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Updated import Validated for screenplay {ScreenplayId}", screenplayId);
                    PublishEvent(passBlobName, msg);
                }
                break;

            case "ChunkSplitDone":
                if (msg.ScreenplayId is { } splitScreenplayId && msg.StartPage is { } startPage && msg.EndPage is { } endPage && msg.TotalPages is { } totalPages)
                {
                    string? sourceBlobName = null;
                    using (var uow = uowFactory.CreateReadWrite("ChunkSplitDone"))
                    {
                        var importQuery = importQueryFactory.Create().ByScreenplayId(splitScreenplayId);
                        var imports = await importRepo.GetByQueryAsync(importQuery, ct);
                        var import = imports.FirstOrDefault();
                        if (import is null)
                        {
                            logger.LogWarning("ScreenplayImport not found for ScreenplayId {ScreenplayId}", splitScreenplayId);
                            break;
                        }
                        sourceBlobName = import.SourceBlobName;

                        var chunkQuery = chunkQueryFactory.Create().ByScreenplayImportId(import.Id).ByStartPage(startPage);
                        var existing = await chunkRepo.GetByQueryAsync(chunkQuery, ct);

                        if (existing.Count == 0)
                        {
                            await importRepo.UpdateByQueryAsync(importQuery, set => set.SetProperty(i => i.TotalPages, totalPages), ct);
                            for (var s = 1; s <= totalPages; s += 10)
                            {
                                var e = Math.Min(s + 9, totalPages);
                                await chunkRepo.AddAsync(ScreenplayImportChunk.Create(import.Id, s, e), ct);
                            }
                            await uow.SaveChangesAsync(ct);
                        }

                        var updateQuery = chunkQueryFactory.Create().ByScreenplayImportId(import.Id).ByStartPage(startPage);
                        await chunkRepo.UpdateByQueryAsync(updateQuery, set => set
                            .SetProperty(c => c.SplitStatus, ChunkStatus.Done)
                            .SetProperty(c => c.SplitCompletedAt, DateTimeOffset.UtcNow), ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Chunk split done: screenplay {ScreenplayId}, pages {Start}-{End}", splitScreenplayId, startPage, endPage);
                    if (sourceBlobName is not null)
                        PublishEvent(sourceBlobName, msg);
                }
                break;

            case "ChunkProcessDone":
                if (msg.ScreenplayId is { } doneScreenplayId && msg.StartPage is { } doneStartPage)
                {
                    string? sourceBlobName = null;
                    int? importTotalPages = null;
                    using (var uow = uowFactory.CreateReadWrite("ChunkProcessDone"))
                    {
                        var imports = await importRepo.GetByQueryAsync(importQueryFactory.Create().ByScreenplayId(doneScreenplayId), ct);
                        var import = imports.FirstOrDefault();
                        if (import is null) break;
                        sourceBlobName = import.SourceBlobName;
                        importTotalPages = import.TotalPages;

                        var query = chunkQueryFactory.Create().ByScreenplayImportId(import.Id).ByStartPage(doneStartPage);
                        await chunkRepo.UpdateByQueryAsync(query, set => set
                            .SetProperty(c => c.ProcessStatus, ChunkStatus.Done)
                            .SetProperty(c => c.ProcessCompletedAt, DateTimeOffset.UtcNow), ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Chunk process done: screenplay {ScreenplayId}, pages from {Start}", doneScreenplayId, doneStartPage);
                    if (sourceBlobName is not null)
                        PublishEvent(sourceBlobName, msg, importTotalPages);
                }
                break;

            case "ChunkProcessFailed":
                if (msg.ScreenplayId is { } failScreenplayId && msg.StartPage is { } failStartPage && msg.Reason is { } failMsgReason)
                {
                    string? sourceBlobName = null;
                    using (var uow = uowFactory.CreateReadWrite("ChunkProcessFailed"))
                    {
                        var imports = await importRepo.GetByQueryAsync(importQueryFactory.Create().ByScreenplayId(failScreenplayId), ct);
                        var import = imports.FirstOrDefault();
                        if (import is null) break;
                        sourceBlobName = import.SourceBlobName;

                        var query = chunkQueryFactory.Create().ByScreenplayImportId(import.Id).ByStartPage(failStartPage);
                        await chunkRepo.UpdateByQueryAsync(query, set => set
                            .SetProperty(c => c.ProcessStatus, ChunkStatus.Failed)
                            .SetProperty(c => c.ProcessErrorMessage, failMsgReason), ct);
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Chunk process failed: screenplay {ScreenplayId}, pages from {Start}", failScreenplayId, failStartPage);
                    if (sourceBlobName is not null)
                        PublishEvent(sourceBlobName, msg);
                }
                break;

            case "ImportFailed":
                if (msg.Reason is { } importFailReason)
                {
                    string? sourceBlobName = null;
                    using (var uow = uowFactory.CreateReadWrite("ImportFailed"))
                    {
                        if (msg.ScreenplayId is { } importFailScreenplayId)
                        {
                            var imports = await importRepo.GetByQueryAsync(importQueryFactory.Create().ByScreenplayId(importFailScreenplayId), ct);
                            sourceBlobName = imports.FirstOrDefault()?.SourceBlobName;

                            var query = importQueryFactory.Create().ByScreenplayId(importFailScreenplayId);
                            await importRepo.UpdateByQueryAsync(query, set => set
                                .SetProperty(i => i.ImportFailedAt, DateTimeOffset.UtcNow)
                                .SetProperty(i => i.ImportErrorMessage, importFailReason), ct);
                        }
                        else if (msg.SourceBlobName is { } importFailBlobName)
                        {
                            sourceBlobName = importFailBlobName;
                            var query = importQueryFactory.Create().BySourceBlobName(importFailBlobName);
                            await importRepo.UpdateByQueryAsync(query, set => set
                                .SetProperty(i => i.ImportFailedAt, DateTimeOffset.UtcNow)
                                .SetProperty(i => i.ImportErrorMessage, importFailReason), ct);
                        }
                        await uow.SaveChangesAsync(ct);
                        await uow.CommitAsync(ct);
                    }
                    logger.LogInformation("Import failed: {Reason}", importFailReason);
                    if (sourceBlobName is not null)
                        PublishEvent(sourceBlobName, msg);
                }
                break;

            default:
                logger.LogWarning("Unknown status message kind: {Kind}", msg.Kind);
                break;
        }
    }

    private void PublishEvent(string blobName, ScreenplayImportStatusMessage msg, int? totalPagesOverride = null)
    {
        var evt = new ImportStatusEvent
        {
            Kind = msg.Kind,
            ScreenplayId = msg.ScreenplayId?.ToString() ?? "",
            ErrorMessage = msg.Reason ?? "",
            StartPage = msg.StartPage ?? 0,
            EndPage = msg.EndPage ?? 0,
            TotalPages = totalPagesOverride ?? msg.TotalPages ?? 0,
        };
        eventBus.Publish(blobName, evt);
    }
}
