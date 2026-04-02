using System.Text.Json;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public sealed class GenerationNode : IDateStamped
{
    private GenerationNode()
    {
    }

    public Guid Id { get; private init; }
    public Guid GenerationRunId { get; private init; }
    public GenerationNodeKind Kind { get; private init; }
    public GenerationNodeStatus Status { get; private set; }
    public JsonDocument? Payload { get; private set; }
    public JsonDocument? AnalysisResult { get; private set; }
    public string? LeaseWorkerId { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime? RunnableAfterUtc { get; private set; }
    public Guid? CurrentAttemptId { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public GenerationRun? GenerationRun { get; private init; }
    public GenerationAttempt? CurrentAttempt { get; private init; }
    public ICollection<GenerationAttempt>? Attempts { get; private init; }
    public ICollection<GeneratedArtifact>? Artifacts { get; private init; }
    public ICollection<GenerationEdge>? OutgoingEdges { get; private init; }
    public ICollection<GenerationEdge>? IncomingEdges { get; private init; }

    public static GenerationNode Create(
        Guid generationRunId,
        GenerationNodeKind kind,
        GenerationNodeStatus initialStatus,
        JsonDocument? payload = null)
    {
        return new GenerationNode
        {
            Id = Guid.CreateVersion7(),
            GenerationRunId = generationRunId,
            Kind = kind,
            Status = initialStatus,
            Payload = payload,
            Attempts = [],
            Artifacts = [],
            OutgoingEdges = [],
            IncomingEdges = []
        };
    }

    public void SetPayload(JsonDocument? payload) => Payload = payload;

    public void SetAnalysisResult(JsonDocument? analysis) => AnalysisResult = analysis;

    public void MarkReady()
    {
        Status = GenerationNodeStatus.Ready;
        RunnableAfterUtc = null;
    }

    public void MarkPending()
    {
        Status = GenerationNodeStatus.Pending;
    }

    public void Claim(string workerId, DateTime leaseExpiresAtUtc, Guid attemptId)
    {
        Status = GenerationNodeStatus.Running;
        LeaseWorkerId = workerId;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        CurrentAttemptId = attemptId;
    }

    public void ReleaseLease()
    {
        LeaseWorkerId = null;
        LeaseExpiresAtUtc = null;
        CurrentAttemptId = null;
    }

    public void MarkSucceeded()
    {
        Status = GenerationNodeStatus.Succeeded;
        ReleaseLease();
        LastErrorMessage = null;
        RunnableAfterUtc = null;
    }

    public void MarkBlocked(string reason)
    {
        Status = GenerationNodeStatus.Blocked;
        LastErrorMessage = reason;
        ReleaseLease();
    }

    public void MarkFailed(string message)
    {
        Status = GenerationNodeStatus.Failed;
        LastErrorMessage = message;
        ReleaseLease();
    }

    public void MarkCancelled()
    {
        Status = GenerationNodeStatus.Cancelled;
        ReleaseLease();
    }

    public void MarkNeedsRetry(string message, DateTime runnableAfterUtc, int maxRetries)
    {
        RetryCount++;
        LastErrorMessage = message;
        ReleaseLease();
        if (RetryCount >= maxRetries)
        {
            Status = GenerationNodeStatus.Failed;
            RunnableAfterUtc = null;
            return;
        }

        Status = GenerationNodeStatus.NeedsRetry;
        RunnableAfterUtc = runnableAfterUtc;
    }

    public void ResetForRegeneration()
    {
        ReleaseLease();
        RetryCount = 0;
        Status = GenerationNodeStatus.Ready;
        RunnableAfterUtc = null;
        LastErrorMessage = null;
        CurrentAttemptId = null;
    }

    public bool LeaseExpired(DateTime utcNow) =>
        Status == GenerationNodeStatus.Running
        && LeaseExpiresAtUtc.HasValue
        && LeaseExpiresAtUtc.Value < utcNow;

    public void ClearStaleLeaseIfExpired(DateTime utcNow)
    {
        if (!LeaseExpired(utcNow))
        {
            return;
        }

        ReleaseLease();
        Status = GenerationNodeStatus.NeedsRetry;
    }
}
