using System.Text.Json;
using Grpc.Core;
using ThePlot.Core.Characters;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.Voices;
using ThePlot.Database.Abstractions;
using ThePlot.Workers.ContentGeneration;

namespace ThePlot.Grpc.Server.Services;

public sealed class ContentGenerationGrpcService(
    ContentGenerationRunService runService,
    ContentGenerationWorkPublisher workPublisher,
    IServiceScopeFactory scopeFactory,
    IUnitOfWorkFactory unitOfWorkFactory,
    IGenerationRunRepository runRepository,
    IGenerationNodeRepository nodeRepository,
    IGeneratedArtifactRepository artifactRepository,
    IQueryFactory<GenerationRun, IGenerationRunQuery> runQueryFactory,
    IQueryFactory<GenerationNode, IGenerationNodeQuery> nodeQueryFactory,
    IQueryFactory<GeneratedArtifact, IGeneratedArtifactQuery> artifactQueryFactory,
    IVoiceRepository voiceRepository,
    ICharacterRepository characterRepository,
    IQueryFactory<Voice, IVoiceQuery> voiceQueryFactory,
    IQueryFactory<Character, ICharacterQuery> characterQueryFactory)
    : ContentGenerationService.ContentGenerationServiceBase
{
    public override async Task<StartRunResponse> StartRun(StartRunRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ScreenplayId, out var screenplayId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid screenplay_id."));
        }

        try
        {
            var runId = await runService.StartRunAsync(screenplayId, request.CancelActive, context.CancellationToken);
            return new StartRunResponse { RunId = runId.ToString() };
        }
        catch (GenerationAlreadyActiveException ex)
        {
            var metadata = new Metadata { { "active_run_id", ex.ActiveRunId.ToString() } };
            throw new RpcException(
                new Status(StatusCode.AlreadyExists, ex.Message),
                metadata);
        }
    }

    public override async Task<CompleteVoiceDeterminationResponse> CompleteVoiceDetermination(
        CompleteVoiceDeterminationRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        var traceParent = await runService.EnqueueVoiceDeterminationAsync(runId, context.CancellationToken);
        await workPublisher.PublishVoiceDeterminationAsync(runId, traceParent, context.CancellationToken);
        return new CompleteVoiceDeterminationResponse();
    }

    public override async Task<ReplayRunResponse> ReplayRun(ReplayRunRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        await runService.ReplayRunAsync(runId, context.CancellationToken);
        await workPublisher.PublishTtsWorkAvailableAsync(runId, null, context.CancellationToken);
        return new ReplayRunResponse();
    }

    public override async Task<RegenerateNodeResponse> RegenerateNode(
        RegenerateNodeRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid node_id."));
        }

        await runService.RegenerateNodeAsync(nodeId, context.CancellationToken);
        await workPublisher.PublishTtsWorkAvailableAsync(null, null, context.CancellationToken);
        return new RegenerateNodeResponse();
    }

    public override async Task<GetRunStatusResponse> GetRunStatus(
        GetRunStatusRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        using var uow = unitOfWorkFactory.CreateReadOnly("GetRunStatus");

        var run = await runRepository.GetByKeyAsync(runId, context.CancellationToken);

        if (run is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Run not found."));
        }

        var nodes = await nodeRepository.GetByQueryAsync(
            nodeQueryFactory.Create().ByRunId(runId).OrderByDateCreated(),
            context.CancellationToken);

        var nodeMessages = nodes.Select(n =>
        {
            var msg = new GenerationNodeStatusMessage
            {
                NodeId = n.Id.ToString(),
                Kind = n.Kind.ToString(),
                Status = n.Status.ToString(),
                RetryCount = n.RetryCount,
                LastError = n.LastErrorMessage ?? "",
            };
            ExtractPayloadInfo(n.Payload, msg);
            return msg;
        }).ToList();

        return new GetRunStatusResponse
        {
            RunId = run.Id.ToString(),
            ScreenplayId = run.ScreenplayId.ToString(),
            Phase = run.Phase.ToString(),
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage ?? "",
            Nodes = { nodeMessages },
        };
    }

    public override async Task<GetRunStatusResponse> GetLatestRunForScreenplay(
        GetLatestRunForScreenplayRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ScreenplayId, out var screenplayId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid screenplay_id."));
        }

        var run = await runRepository.GetFirstByQueryAsync(
            runQueryFactory.Create().ByScreenplayId(screenplayId).OrderByDateCreatedDescending(),
            context.CancellationToken);

        if (run is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "No generation run found for this screenplay."));
        }

        var nodes = await nodeRepository.GetByQueryAsync(
            nodeQueryFactory.Create().ByRunId(run.Id).OrderByDateCreated(),
            context.CancellationToken);

        var nodeMessages = nodes.Select(n =>
        {
            var msg = new GenerationNodeStatusMessage
            {
                NodeId = n.Id.ToString(),
                Kind = n.Kind.ToString(),
                Status = n.Status.ToString(),
                RetryCount = n.RetryCount,
                LastError = n.LastErrorMessage ?? "",
            };
            ExtractPayloadInfo(n.Payload, msg);
            return msg;
        }).ToList();

        return new GetRunStatusResponse
        {
            RunId = run.Id.ToString(),
            ScreenplayId = run.ScreenplayId.ToString(),
            Phase = run.Phase.ToString(),
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage ?? "",
            Nodes = { nodeMessages },
        };
    }

    public override async Task<GetNodeAudioResponse> GetNodeAudio(
        GetNodeAudioRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid node_id."));
        }

        var artifact = await artifactRepository.GetFirstByQueryAsync(
            artifactQueryFactory.Create().ByNodeId(nodeId).ByIsCurrent(true),
            context.CancellationToken);

        if (artifact?.Metadata is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "No audio artifact found for this node."));
        }

        var root = artifact.Metadata.RootElement;
        var audioBase64 = root.TryGetProperty("AudioBase64", out var ab) ? ab.GetString() ?? "" : "";
        var audioFormat = root.TryGetProperty("AudioFormat", out var af) ? af.GetString() ?? "" : "";

        return new GetNodeAudioResponse
        {
            AudioBase64 = audioBase64,
            AudioFormat = audioFormat,
            MimeType = artifact.MimeType ?? $"audio/{audioFormat}",
        };
    }

    public override async Task StreamRunStatus(
        StreamRunStatusRequest request,
        IServerStreamWriter<RunStatusUpdate> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        var previousStatuses = new Dictionary<Guid, string>();

        while (!context.CancellationToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scopedRunRepo = scope.ServiceProvider.GetRequiredService<IGenerationRunRepository>();
            var scopedNodeRepo = scope.ServiceProvider.GetRequiredService<IGenerationNodeRepository>();
            var scopedNodeQueryFactory = scope.ServiceProvider.GetRequiredService<IQueryFactory<GenerationNode, IGenerationNodeQuery>>();
            var scopedUowFactory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

            using var uow = scopedUowFactory.CreateReadOnly("StreamRunStatus");

            var run = await scopedRunRepo.GetByKeyAsync(runId, context.CancellationToken);

            if (run is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Run not found."));
            }

            var nodes = await scopedNodeRepo.GetByQueryAsync(
                scopedNodeQueryFactory.Create().ByRunId(runId).OrderByDateCreated(),
                context.CancellationToken);

            var changedNodes = new List<GenerationNodeStatusMessage>();

            foreach (var node in nodes)
            {
                var currentStatus = $"{node.Status}:{node.RetryCount}:{node.LastErrorMessage}";
                var hasChanged = !previousStatuses.TryGetValue(node.Id, out var prev) || prev != currentStatus;

                if (hasChanged)
                {
                    previousStatuses[node.Id] = currentStatus;
                    var msg = new GenerationNodeStatusMessage
                    {
                        NodeId = node.Id.ToString(),
                        Kind = node.Kind.ToString(),
                        Status = node.Status.ToString(),
                        RetryCount = node.RetryCount,
                        LastError = node.LastErrorMessage ?? "",
                    };
                    ExtractPayloadInfo(node.Payload, msg);
                    changedNodes.Add(msg);
                }
            }

            if (changedNodes.Count > 0 || previousStatuses.Count == 0)
            {
                var update = new RunStatusUpdate
                {
                    RunId = run.Id.ToString(),
                    RunStatus = run.Status.ToString(),
                    Phase = run.Phase.ToString(),
                    ErrorMessage = run.ErrorMessage ?? "",
                };
                update.Nodes.AddRange(changedNodes);
                await responseStream.WriteAsync(update, context.CancellationToken);
            }

            var isTerminal = run.Status is GenerationRunStatus.Completed
                or GenerationRunStatus.Failed
                or GenerationRunStatus.Cancelled;

            var allNodesTerminal = nodes.Count > 0 && nodes.All(n =>
                n.Status is GenerationNodeStatus.Succeeded
                    or GenerationNodeStatus.Failed
                    or GenerationNodeStatus.Blocked
                    or GenerationNodeStatus.Cancelled);

            if (isTerminal || (allNodesTerminal && nodes.Count > 0))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), context.CancellationToken);
        }
    }

    public override async Task<ListRunsForScreenplayResponse> ListRunsForScreenplay(
        ListRunsForScreenplayRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ScreenplayId, out var screenplayId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid screenplay_id."));
        }

        var runs = await runRepository.GetByQueryAsync(
            runQueryFactory.Create().ByScreenplayId(screenplayId).OrderByDateCreatedDescending(),
            context.CancellationToken);

        var runIds = runs.Select(r => r.Id).ToList();

        var allNodes = await nodeRepository.GetByQueryAsync(
            nodeQueryFactory.Create().ByRunIds(runIds),
            context.CancellationToken);

        var nodeCounts = allNodes
            .GroupBy(n => n.GenerationRunId)
            .ToDictionary(g => g.Key, g => new
            {
                Total = g.Count(n => n.Kind != GenerationNodeKind.PreGenerationAnalysis),
                Succeeded = g.Count(n => n.Kind != GenerationNodeKind.PreGenerationAnalysis && n.Status == GenerationNodeStatus.Succeeded),
                Failed = g.Count(n => n.Kind != GenerationNodeKind.PreGenerationAnalysis && n.Status == GenerationNodeStatus.Failed),
            });

        var response = new ListRunsForScreenplayResponse();
        foreach (var run in runs)
        {
            nodeCounts.TryGetValue(run.Id, out var counts);
            response.Runs.Add(new RunSummary
            {
                RunId = run.Id.ToString(),
                Status = run.Status.ToString(),
                Phase = run.Phase.ToString(),
                CreatedAt = run.DateCreated.ToString("O"),
                TotalNodes = counts?.Total ?? 0,
                SucceededNodes = counts?.Succeeded ?? 0,
                FailedNodes = counts?.Failed ?? 0,
            });
        }

        return response;
    }

    public override async Task<GetRunDetailsResponse> GetRunDetails(
        GetRunDetailsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        using var uow = unitOfWorkFactory.CreateReadOnly("GetRunDetails");

        var run = await runRepository.GetByKeyAsync(runId, context.CancellationToken);

        if (run is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Run not found."));
        }

        var nodes = await nodeRepository.GetByQueryAsync(
            nodeQueryFactory.Create().ByRunId(runId).OrderByDateCreated(),
            context.CancellationToken);

        var voiceIdSet = new HashSet<Guid>();
        var characterIdSet = new HashSet<Guid>();
        foreach (var node in nodes)
        {
            ExtractIdsFromPayload(node.Payload, voiceIdSet, characterIdSet);
        }

        var voiceMap = voiceIdSet.Count > 0
            ? (await voiceRepository.GetByQueryAsync(
                voiceQueryFactory.Create().ByScreenplayId(run.ScreenplayId),
                context.CancellationToken))
                .Where(v => voiceIdSet.Contains(v.Id))
                .ToDictionary(v => v.Id)
            : new Dictionary<Guid, Voice>();

        var characterMap = characterIdSet.Count > 0
            ? (await characterRepository.GetByQueryAsync(
                characterQueryFactory.Create().ByIds(characterIdSet),
                context.CancellationToken))
                .ToDictionary(c => c.Id, c => c.Name)
            : new Dictionary<Guid, string>();

        var response = new GetRunDetailsResponse
        {
            RunId = run.Id.ToString(),
            ScreenplayId = run.ScreenplayId.ToString(),
            Phase = run.Phase.ToString(),
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage ?? "",
            CreatedAt = run.DateCreated.ToString("O"),
        };

        foreach (var node in nodes)
        {
            var detail = BuildNodeDetail(node, voiceMap, characterMap);
            response.Nodes.Add(detail);
        }

        return response;
    }

    private static GenerationNodeDetail BuildNodeDetail(
        GenerationNode node,
        Dictionary<Guid, Voice> voiceMap,
        Dictionary<Guid, string> characterMap)
    {
        var detail = new GenerationNodeDetail
        {
            NodeId = node.Id.ToString(),
            Kind = node.Kind.ToString(),
            Status = node.Status.ToString(),
            RetryCount = node.RetryCount,
            LastError = node.LastErrorMessage ?? "",
        };

        if (node.Payload is null) return detail;

        var root = node.Payload.RootElement;

        if (root.TryGetProperty("sceneId", out var sceneId))
        {
            detail.SceneId = sceneId.GetString() ?? "";
        }

        if (root.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                var eid = line.TryGetProperty("elementId", out var e) ? e.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(eid)) detail.ElementIds.Add(eid);

                var lineDetail = new NodeLineDetail
                {
                    ElementId = eid,
                    Type = line.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                    Text = line.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "",
                };

                if (line.TryGetProperty("characterId", out var cid) && Guid.TryParse(cid.GetString(), out var characterId))
                {
                    lineDetail.CharacterName = characterMap.TryGetValue(characterId, out var cname) ? cname : "";
                }

                if (line.TryGetProperty("voiceId", out var vid) && Guid.TryParse(vid.GetString(), out var voiceId)
                    && voiceMap.TryGetValue(voiceId, out var voice))
                {
                    lineDetail.VoiceName = voice.Name;
                    lineDetail.VoiceDescription = voice.Description;
                }

                detail.Lines.Add(lineDetail);
            }
        }
        else
        {
            if (root.TryGetProperty("elementId", out var elementId))
            {
                detail.ElementIds.Add(elementId.GetString() ?? "");
            }

            detail.Text = root.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";

            if (root.TryGetProperty("voiceId", out var voiceIdProp) && Guid.TryParse(voiceIdProp.GetString(), out var vid)
                && voiceMap.TryGetValue(vid, out var voice))
            {
                detail.VoiceName = voice.Name;
                detail.VoiceDescription = voice.Description;
            }
        }

        return detail;
    }

    private static void ExtractIdsFromPayload(
        JsonDocument? payload,
        HashSet<Guid> voiceIds,
        HashSet<Guid> characterIds)
    {
        if (payload is null) return;
        var root = payload.RootElement;

        if (root.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                if (line.TryGetProperty("voiceId", out var vid) && Guid.TryParse(vid.GetString(), out var voiceId))
                    voiceIds.Add(voiceId);
                if (line.TryGetProperty("characterId", out var cid) && Guid.TryParse(cid.GetString(), out var characterId))
                    characterIds.Add(characterId);
            }
        }
        else
        {
            if (root.TryGetProperty("voiceId", out var vid) && Guid.TryParse(vid.GetString(), out var voiceId))
                voiceIds.Add(voiceId);
        }
    }

    private static void ExtractPayloadInfo(JsonDocument? payload, GenerationNodeStatusMessage msg)
    {
        if (payload is null) return;

        var root = payload.RootElement;

        if (root.TryGetProperty("sceneId", out var sceneId))
        {
            msg.SceneId = sceneId.GetString() ?? "";
        }

        if (root.TryGetProperty("elementId", out var elementId))
        {
            msg.ElementIds.Add(elementId.GetString() ?? "");
        }

        if (root.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                if (line.TryGetProperty("elementId", out var eid))
                {
                    msg.ElementIds.Add(eid.GetString() ?? "");
                }
            }
        }
    }
}
