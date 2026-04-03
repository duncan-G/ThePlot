using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

internal sealed class GeneratedArtifactQuery(ThePlotContext context) : IGeneratedArtifactQuery, IExecutableQuery<GeneratedArtifact>
{
    private IQueryable<GeneratedArtifact> _query = context.GeneratedArtifacts.AsNoTracking();

    IQueryable<GeneratedArtifact> IExecutableQuery<GeneratedArtifact>.AsQueryable() => _query;

    public IGeneratedArtifactQuery ByNodeId(Guid nodeId)
    {
        _query = _query.Where(a => a.GenerationNodeId == nodeId);
        return this;
    }

    public IGeneratedArtifactQuery ByIsCurrent(bool isCurrent)
    {
        _query = _query.Where(a => a.IsCurrent == isCurrent);
        return this;
    }
}
