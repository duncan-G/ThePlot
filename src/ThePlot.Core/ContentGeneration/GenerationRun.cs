using ThePlot.Core.Screenplays;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public sealed class GenerationRun : IDateStamped
{
    private GenerationRun()
    {
    }

    public Guid Id { get; private init; }
    public Guid ScreenplayId { get; private init; }
    public GenerationWorkflowPhase Phase { get; private set; }
    public GenerationRunStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public Screenplay? Screenplay { get; private init; }
    public ICollection<GenerationNode>? Nodes { get; private init; }

    public static GenerationRun Start(Guid screenplayId)
    {
        return new GenerationRun
        {
            Id = Guid.CreateVersion7(),
            ScreenplayId = screenplayId,
            Phase = GenerationWorkflowPhase.CharacterResolution,
            Status = GenerationRunStatus.Pending,
            Nodes = []
        };
    }

    public void MarkRunning()
    {
        Status = GenerationRunStatus.Running;
        ErrorMessage = null;
    }

    public void MarkCompleted()
    {
        Status = GenerationRunStatus.Completed;
        ErrorMessage = null;
    }

    public void MarkFailed(string message)
    {
        Status = GenerationRunStatus.Failed;
        ErrorMessage = message;
    }

    public void AdvanceToContentGenerationPhase()
    {
        Phase = GenerationWorkflowPhase.ContentGeneration;
    }
}
