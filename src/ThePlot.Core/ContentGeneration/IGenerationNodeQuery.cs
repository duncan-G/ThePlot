using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public interface IGenerationNodeQuery : IQuery<GenerationNode>
{
    IGenerationNodeQuery ById(Guid id);
    IGenerationNodeQuery ByRunId(Guid runId);
    IGenerationNodeQuery ByRunIds(IEnumerable<Guid> runIds);
    IGenerationNodeQuery ByStatus(GenerationNodeStatus status);
    IGenerationNodeQuery ByNotStatus(GenerationNodeStatus status);
    IGenerationNodeQuery ByKind(GenerationNodeKind kind);
    IGenerationNodeQuery OrderByDateCreated();
    IGenerationNodeQuery IncludeRun();
    IGenerationNodeQuery IncludeArtifacts();
    IGenerationNodeQuery IncludeAttempts();
}
