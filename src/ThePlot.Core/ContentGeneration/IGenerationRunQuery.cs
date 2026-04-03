using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public interface IGenerationRunQuery : IQuery<GenerationRun>
{
    IGenerationRunQuery ByScreenplayId(Guid screenplayId);
    IGenerationRunQuery ByStatus(GenerationRunStatus status);
    IGenerationRunQuery ByStatuses(IEnumerable<GenerationRunStatus> statuses);
    IGenerationRunQuery ByPhase(GenerationWorkflowPhase phase);
    IGenerationRunQuery ByPhaseAndStatus(GenerationWorkflowPhase phase, GenerationRunStatus status);
    IGenerationRunQuery OrderByDateCreated();
    IGenerationRunQuery OrderByDateCreatedDescending();
}
