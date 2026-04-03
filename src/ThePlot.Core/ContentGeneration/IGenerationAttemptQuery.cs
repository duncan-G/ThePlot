using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public interface IGenerationAttemptQuery : IQuery<GenerationAttempt>
{
    IGenerationAttemptQuery ByNodeId(Guid nodeId);
}
