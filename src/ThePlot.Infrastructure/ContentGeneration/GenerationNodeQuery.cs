using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

internal sealed class GenerationNodeQuery(ThePlotContext context) : IGenerationNodeQuery, IExecutableQuery<GenerationNode>
{
    private IQueryable<GenerationNode> _query = context.GenerationNodes.AsNoTracking();

    IQueryable<GenerationNode> IExecutableQuery<GenerationNode>.AsQueryable() => _query;

    public IGenerationNodeQuery ById(Guid id)
    {
        _query = _query.Where(n => n.Id == id);
        return this;
    }

    public IGenerationNodeQuery ByRunId(Guid runId)
    {
        _query = _query.Where(n => n.GenerationRunId == runId);
        return this;
    }

    public IGenerationNodeQuery ByRunIds(IEnumerable<Guid> runIds)
    {
        var ids = runIds.ToList();
        _query = _query.Where(n => ids.Contains(n.GenerationRunId));
        return this;
    }

    public IGenerationNodeQuery ByStatus(GenerationNodeStatus status)
    {
        _query = _query.Where(n => n.Status == status);
        return this;
    }

    public IGenerationNodeQuery ByNotStatus(GenerationNodeStatus status)
    {
        _query = _query.Where(n => n.Status != status);
        return this;
    }

    public IGenerationNodeQuery ByKind(GenerationNodeKind kind)
    {
        _query = _query.Where(n => n.Kind == kind);
        return this;
    }

    public IGenerationNodeQuery OrderByDateCreated()
    {
        _query = _query.OrderBy(n => n.DateCreated);
        return this;
    }

    public IGenerationNodeQuery IncludeRun()
    {
        _query = _query.Include(n => n.GenerationRun);
        return this;
    }

    public IGenerationNodeQuery IncludeArtifacts()
    {
        _query = _query.Include(n => n.Artifacts);
        return this;
    }

    public IGenerationNodeQuery IncludeAttempts()
    {
        _query = _query.Include(n => n.Attempts);
        return this;
    }
}
