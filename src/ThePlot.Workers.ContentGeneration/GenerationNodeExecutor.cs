#pragma warning disable MEAI001
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;
using ThePlot.Infrastructure.ContentGeneration;

namespace ThePlot.Workers.ContentGeneration;

public sealed class GenerationNodeExecutor(
    IUnitOfWorkFactory unitOfWorkFactory,
    IGenerationRunRepository runRepository,
    IGenerationNodeRepository nodeRepository,
    IGenerationAttemptRepository attemptRepository,
    IGeneratedArtifactRepository artifactRepository,
    IQueryFactory<GenerationNode, IGenerationNodeQuery> nodeQueryFactory,
    IQueryFactory<GeneratedArtifact, IGeneratedArtifactQuery> artifactQueryFactory,
    ITextToSpeechClient ttsClient,
    IOptions<ContentGenerationOptions> options,
    ILogger<GenerationNodeExecutor> logger)
{
    public async Task ExecuteAsync(ClaimedGenerationWork work, string workerId, CancellationToken ct)
    {
        using var uow = unitOfWorkFactory.CreateReadWrite("ExecuteNode");
        var opt = options.Value;

        var node = await nodeRepository.GetByKeyAsync(work.NodeId, ct)
                   ?? throw new InvalidOperationException($"Generation node {work.NodeId} not found.");
        var run = await runRepository.GetByKeyAsync(node.GenerationRunId, ct);

        using var activity = ContentGenerationTelemetry.StartActivity(
            "ContentGeneration.ExecuteNode", run?.TraceParent);
        activity?.SetTag("contentgen.run_id", node.GenerationRunId.ToString());
        activity?.SetTag("contentgen.node_id", node.Id.ToString());
        activity?.SetTag("contentgen.node_kind", node.Kind.ToString());
        activity?.SetTag("contentgen.worker_id", workerId);
        activity?.SetTag("contentgen.attempt_id", work.AttemptId.ToString());

        if (node.LeaseWorkerId != workerId || node.CurrentAttemptId != work.AttemptId)
        {
            logger.LogWarning(
                "Skipping stale work for node {NodeId}; lease or attempt mismatch.",
                work.NodeId);
            activity?.SetTag("contentgen.skipped", true);
            return;
        }

        if (run?.Status == GenerationRunStatus.Cancelled
            || node.Status == GenerationNodeStatus.Cancelled)
        {
            logger.LogInformation("Skipping node {NodeId}; run or node was cancelled.", work.NodeId);
            activity?.SetTag("contentgen.skipped_cancelled", true);
            return;
        }

        var attempt = await attemptRepository.GetByKeyAsync(work.AttemptId, ct)
                      ?? throw new InvalidOperationException($"Generation attempt {work.AttemptId} not found.");

        try
        {
            switch (node.Kind)
            {
                case GenerationNodeKind.PreGenerationAnalysis:
                    ExecuteAnalysis(node, attempt, run);
                    break;
                case GenerationNodeKind.VoicePromptBatch:
                case GenerationNodeKind.ActionElement:
                case GenerationNodeKind.VoiceOverElement:
                    await ExecuteTtsAsync(node, attempt, ct);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported node kind {node.Kind}.");
            }

            activity?.SetTag("contentgen.node_result", "succeeded");
            await uow.SaveChangesAsync(ct);
            await TryCompleteRunIfAllSucceededAsync(node.GenerationRunId, uow, ct);
        }
        catch (Exception ex)
        {
            ContentGenerationTelemetry.RecordError(activity, ex);
            logger.LogWarning(ex, "Generation node {NodeId} attempt {AttemptId} failed.", work.NodeId, work.AttemptId);
            attempt.MarkFailed(ex.Message);
            var backoffSeconds = Math.Pow(2, node.RetryCount) * opt.RetryBackoffBase.TotalSeconds;
            var runnableAfter = DateTime.UtcNow.AddSeconds(Math.Clamp(backoffSeconds, 1, 3600));
            node.MarkNeedsRetry(ex.Message, runnableAfter, opt.MaxRetryAttemptsPerNode);
            await uow.SaveChangesAsync(ct);

            if (node.Status == GenerationNodeStatus.Failed)
            {
                activity?.SetTag("contentgen.node_result", "failed_permanently");
                await MarkRunFailedIfNeededAsync(node.GenerationRunId, ex.Message, uow, ct);
            }
            else
            {
                activity?.SetTag("contentgen.node_result", "needs_retry");
                activity?.SetTag("contentgen.retry_count", node.RetryCount);
            }
        }

        await uow.CommitAsync(ct);
    }

    private static void ExecuteAnalysis(GenerationNode node, GenerationAttempt attempt, GenerationRun? run)
    {
        if (node.AnalysisResult is null)
        {
            throw new InvalidOperationException("Analysis node is missing AnalysisResult payload.");
        }

        var speakable = node.AnalysisResult.RootElement.TryGetProperty("speakable", out var sp) && sp.GetBoolean();
        if (!speakable)
        {
            var reason = node.AnalysisResult.RootElement.TryGetProperty("blockingReasons", out var br)
                ? string.Join("; ", br.EnumerateArray().Select(e => e.GetString()).OfType<string>())
                : "Speakability check failed.";
            node.MarkBlocked(reason);
            attempt.MarkFailed(reason);
            run?.MarkFailed(reason);
            return;
        }

        attempt.MarkSucceeded(JsonSerializer.SerializeToDocument(new { ok = true }));
        node.MarkSucceeded();
    }

    private async Task ExecuteTtsAsync(GenerationNode node, GenerationAttempt attempt, CancellationToken ct)
    {
        var opt = options.Value;
        var prompt = BuildTtsPrompt(node);
        attempt.SetProviderRequestJson(JsonSerializer.SerializeToDocument(new { prompt, node.Kind }));

        var response = await ttsClient.GetAudioAsync(prompt, cancellationToken: ct);
        var audioContent = response.Contents.OfType<DataContent>().FirstOrDefault();
        var bytes = audioContent?.Data.ToArray() ?? [];
        var audioRaw = Convert.ToBase64String(bytes);
        var mediaType = audioContent?.MediaType ?? "audio/wav";
        var audioFormat = mediaType.Split('/').LastOrDefault() ?? "wav";
        if (audioFormat == "mpeg") audioFormat = "mp3";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(audioRaw)));

        var usage = TokenUsage.None;
        attempt.RecordUsage(usage, opt.TtsPricing.CalculateCost(usage));

        var artifact = GeneratedArtifact.Create(
            node.Id,
            attempt.Id,
            isCurrent: true,
            storageUri: $"inline:{mediaType};base64",
            mimeType: mediaType,
            contentHash: hash,
            metadata: JsonSerializer.SerializeToDocument(
                new { AudioBase64 = audioRaw, AudioFormat = audioFormat, lengthChars = prompt.Length }));

        var currentArtifacts = await artifactRepository.GetByQueryAsync(
            artifactQueryFactory.Create().ByNodeId(node.Id).ByIsCurrent(true), ct);

        foreach (var old in currentArtifacts)
        {
            old.Supersede();
        }

        await artifactRepository.AddAsync(artifact, ct);
        attempt.MarkSucceeded(
            JsonSerializer.SerializeToDocument(new { format = audioFormat, contentHash = hash }));

        node.MarkSucceeded();
    }

    private static string BuildTtsPrompt(GenerationNode node)
    {
        if (node.Payload is null)
        {
            return string.Empty;
        }

        var root = node.Payload.RootElement;
        string raw;

        if (node.Kind == GenerationNodeKind.VoicePromptBatch &&
            root.TryGetProperty("lines", out var lines) &&
            lines.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var line in lines.EnumerateArray())
            {
                var text = line.TryGetProperty("text", out var t) ? t.GetString() : "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            raw = sb.ToString().Trim();
        }
        else
        {
            raw = root.TryGetProperty("text", out var te) ? te.GetString() ?? "" : "";
        }

        return TextNormalizer.Normalize(raw);
    }

    private async Task TryCompleteRunIfAllSucceededAsync(Guid runId, IUnitOfWork uow, CancellationToken ct)
    {
        var hasPending = await nodeRepository.ExistsByQueryAsync(
            nodeQueryFactory.Create().ByRunId(runId).ByNotStatus(GenerationNodeStatus.Succeeded), ct);

        if (!hasPending)
        {
            var run = await runRepository.GetByKeyAsync(runId, ct)
                      ?? throw new InvalidOperationException($"Generation run {runId} not found.");
            run.MarkCompleted();
            await uow.SaveChangesAsync(ct);
        }
    }

    private async Task MarkRunFailedIfNeededAsync(Guid runId, string message, IUnitOfWork uow, CancellationToken ct)
    {
        var run = await runRepository.GetByKeyAsync(runId, ct);
        if (run is null)
        {
            return;
        }

        run.MarkFailed($"Node failed after retries: {message}");
        await uow.SaveChangesAsync(ct);
    }
}
