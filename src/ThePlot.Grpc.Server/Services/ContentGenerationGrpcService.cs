using System.Text.Json;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Infrastructure;
using ThePlot.Workers.ContentGeneration;

namespace ThePlot.Grpc.Server.Services;

public sealed class ContentGenerationGrpcService(
    ContentGenerationRunService runService,
    IServiceScopeFactory scopeFactory,
    ThePlotContext db)
    : ContentGenerationService.ContentGenerationServiceBase
{
    public override async Task<StartRunResponse> StartRun(StartRunRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ScreenplayId, out var screenplayId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid screenplay_id."));
        }

        var runId = await runService.StartRunAsync(screenplayId, context.CancellationToken);
        return new StartRunResponse { RunId = runId.ToString() };
    }

    public override async Task<CompleteVoiceDeterminationResponse> CompleteVoiceDetermination(
        CompleteVoiceDeterminationRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        await runService.CompleteVoiceDeterminationAndStartContentGenerationAsync(runId, context.CancellationToken);
        return new CompleteVoiceDeterminationResponse();
    }

    public override async Task<ReplayRunResponse> ReplayRun(ReplayRunRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid run_id."));
        }

        await runService.ReplayRunAsync(runId, context.CancellationToken);
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

        var run = await db.GenerationRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, context.CancellationToken);

        if (run is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Run not found."));
        }

        var nodes = await db.GenerationNodes.AsNoTracking()
            .Where(n => n.GenerationRunId == runId)
            .OrderBy(n => n.DateCreated)
            .Select(n => new GenerationNodeStatusMessage
            {
                NodeId = n.Id.ToString(),
                Kind = n.Kind.ToString(),
                Status = n.Status.ToString(),
                RetryCount = n.RetryCount,
                LastError = n.LastErrorMessage ?? "",
            })
            .ToListAsync(context.CancellationToken);

        return new GetRunStatusResponse
        {
            RunId = run.Id.ToString(),
            ScreenplayId = run.ScreenplayId.ToString(),
            Phase = run.Phase.ToString(),
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage ?? "",
            Nodes = { nodes },
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
            var scopedDb = scope.ServiceProvider.GetRequiredService<ThePlotContext>();

            var run = await scopedDb.GenerationRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == runId, context.CancellationToken);

            if (run is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Run not found."));
            }

            var nodes = await scopedDb.GenerationNodes.AsNoTracking()
                .Where(n => n.GenerationRunId == runId)
                .OrderBy(n => n.DateCreated)
                .ToListAsync(context.CancellationToken);

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
