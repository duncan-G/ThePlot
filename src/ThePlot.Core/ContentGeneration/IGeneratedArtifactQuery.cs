using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public interface IGeneratedArtifactQuery : IQuery<GeneratedArtifact>
{
    IGeneratedArtifactQuery ByNodeId(Guid nodeId);
    IGeneratedArtifactQuery ByIsCurrent(bool isCurrent);
}
