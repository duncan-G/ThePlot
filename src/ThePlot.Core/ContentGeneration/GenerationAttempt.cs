using System.Text.Json;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public sealed class GenerationAttempt : IDateStamped
{
    private GenerationAttempt()
    {
    }

    public Guid Id { get; private init; }
    public Guid GenerationNodeId { get; private init; }
    public int AttemptNumber { get; private init; }
    public GenerationAttemptStatus Status { get; private set; }
    public JsonDocument? ProviderRequestJson { get; private set; }
    public JsonDocument? ProviderResponseJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public GenerationNode? GenerationNode { get; private init; }
    public ICollection<GeneratedArtifact>? Artifacts { get; private init; }

    public static GenerationAttempt Create(Guid generationNodeId, int attemptNumber)
    {
        return new GenerationAttempt
        {
            Id = Guid.CreateVersion7(),
            GenerationNodeId = generationNodeId,
            AttemptNumber = attemptNumber,
            Status = GenerationAttemptStatus.Pending,
            Artifacts = []
        };
    }

    public void MarkRunning()
    {
        Status = GenerationAttemptStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkSucceeded(JsonDocument? responseJson = null)
    {
        Status = GenerationAttemptStatus.Succeeded;
        ProviderResponseJson = responseJson;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string message, JsonDocument? responseJson = null)
    {
        Status = GenerationAttemptStatus.Failed;
        ErrorMessage = message;
        ProviderResponseJson = responseJson;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void SetProviderRequestJson(JsonDocument? json) => ProviderRequestJson = json;
}
