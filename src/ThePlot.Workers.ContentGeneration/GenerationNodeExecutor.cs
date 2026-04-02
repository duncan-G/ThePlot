using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePlot.Core.ContentGeneration;
using ThePlot.Infrastructure;
using ThePlot.Infrastructure.ContentGeneration;
using ThePlot.Infrastructure.Tts;

namespace ThePlot.Workers.ContentGeneration;

public sealed class GenerationNodeExecutor(
    ThePlotContext db,
    ITtsSpeechClient ttsSpeechClient,
    IOptions<ContentGenerationOptions> options,
    ILogger<GenerationNodeExecutor> logger)
{
    public async Task ExecuteAsync(ClaimedGenerationWork work, string workerId, CancellationToken ct)
    {
        var opt = options.Value;
        var node = await db.GenerationNodes
            .Include(n => n.GenerationRun)
            .FirstAsync(n => n.Id == work.NodeId, ct);

        if (node.LeaseWorkerId != workerId || node.CurrentAttemptId != work.AttemptId)
        {
            logger.LogWarning(
                "Skipping stale work for node {NodeId}; lease or attempt mismatch.",
                work.NodeId);
            return;
        }

        var attempt = await db.GenerationAttempts.FirstAsync(a => a.Id == work.AttemptId, ct);

        try
        {
            switch (node.Kind)
            {
                case GenerationNodeKind.PreGenerationAnalysis:
                    await ExecuteAnalysisAsync(node, attempt, ct);
                    break;
                case GenerationNodeKind.VoicePromptBatch:
                case GenerationNodeKind.ActionElement:
                case GenerationNodeKind.VoiceOverElement:
                    await ExecuteTtsAsync(node, attempt, ct);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported node kind {node.Kind}.");
            }

            await db.SaveChangesAsync(ct);
            await TryCompleteRunIfAllSucceededAsync(node.GenerationRunId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Generation node {NodeId} attempt {AttemptId} failed.", work.NodeId, work.AttemptId);
            attempt.MarkFailed(ex.Message);
            var backoffSeconds = Math.Pow(2, node.RetryCount) * opt.RetryBackoffBase.TotalSeconds;
            var runnableAfter = DateTime.UtcNow.AddSeconds(Math.Clamp(backoffSeconds, 1, 3600));
            node.MarkNeedsRetry(ex.Message, runnableAfter, opt.MaxRetryAttemptsPerNode);
            await db.SaveChangesAsync(ct);

            if (node.Status == GenerationNodeStatus.Failed)
            {
                await MarkRunFailedIfNeededAsync(node.GenerationRunId, ex.Message, ct);
            }
        }
    }

    private Task ExecuteAnalysisAsync(GenerationNode node, GenerationAttempt attempt, CancellationToken ct)
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
            if (node.GenerationRun is not null)
            {
                node.GenerationRun.MarkFailed(reason);
            }

            return Task.CompletedTask;
        }

        attempt.MarkSucceeded(JsonSerializer.SerializeToDocument(new { ok = true }));
        node.MarkSucceeded();
        return Task.CompletedTask;
    }

    private async Task ExecuteTtsAsync(GenerationNode node, GenerationAttempt attempt, CancellationToken ct)
    {
        var prompt = BuildTtsPrompt(node);
        attempt.SetProviderRequestJson(JsonSerializer.SerializeToDocument(new { prompt, node.Kind }));

        var result = await ttsSpeechClient.GetSpeechAsync(prompt, ct);
        var audioRaw = result.AudioBase64 ?? "";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(audioRaw)));

        var artifact = GeneratedArtifact.Create(
            node.Id,
            attempt.Id,
            isCurrent: true,
            storageUri: $"inline:audio/{result.AudioFormat};base64",
            mimeType: $"audio/{result.AudioFormat}",
            contentHash: hash,
            metadata: JsonSerializer.SerializeToDocument(
                new { result.AudioBase64, result.AudioFormat, lengthChars = prompt.Length }));

        foreach (var old in await db.GeneratedArtifacts
                     .Where(a => a.GenerationNodeId == node.Id && a.IsCurrent)
                     .ToListAsync(ct))
        {
            old.Supersede();
        }

        db.GeneratedArtifacts.Add(artifact);
        attempt.MarkSucceeded(
            JsonSerializer.SerializeToDocument(new { format = result.AudioFormat, contentHash = hash }));

        node.MarkSucceeded();
    }

    private static string BuildTtsPrompt(GenerationNode node)
    {
        if (node.Payload is null)
        {
            return string.Empty;
        }

        var root = node.Payload.RootElement;
        if (node.Kind == GenerationNodeKind.VoicePromptBatch &&
            root.TryGetProperty("lines", out var lines) &&
            lines.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var line in lines.EnumerateArray())
            {
                var voice = line.TryGetProperty("voiceId", out var s) ? s.GetString() : "?";
                var text = line.TryGetProperty("text", out var t) ? t.GetString() : "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append("Voice ").Append(voice).Append(": ").AppendLine(text);
                }
            }

            return sb.ToString().Trim();
        }

        return root.TryGetProperty("text", out var te) ? te.GetString() ?? "" : "";
    }

    private async Task TryCompleteRunIfAllSucceededAsync(Guid runId, CancellationToken ct)
    {
        var hasPending = await db.GenerationNodes
            .AnyAsync(n => n.GenerationRunId == runId && n.Status != GenerationNodeStatus.Succeeded, ct);

        if (!hasPending)
        {
            var run = await db.GenerationRuns.FirstAsync(r => r.Id == runId, ct);
            run.MarkCompleted();
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task MarkRunFailedIfNeededAsync(Guid runId, string message, CancellationToken ct)
    {
        var run = await db.GenerationRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return;
        }

        run.MarkFailed($"Node failed after retries: {message}");
        await db.SaveChangesAsync(ct);
    }
}
