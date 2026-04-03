using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

internal sealed class GenerationRunQuery(ThePlotContext context) : IGenerationRunQuery, IExecutableQuery<GenerationRun>
{
    private IQueryable<GenerationRun> _query = context.GenerationRuns.AsNoTracking();

    IQueryable<GenerationRun> IExecutableQuery<GenerationRun>.AsQueryable() => _query;

    public IGenerationRunQuery ByScreenplayId(Guid screenplayId)
    {
        _query = _query.Where(r => r.ScreenplayId == screenplayId);
        return this;
    }

    public IGenerationRunQuery ByStatus(GenerationRunStatus status)
    {
        _query = _query.Where(r => r.Status == status);
        return this;
    }

    public IGenerationRunQuery ByStatuses(IEnumerable<GenerationRunStatus> statuses)
    {
        var list = statuses.ToList();
        _query = _query.Where(r => list.Contains(r.Status));
        return this;
    }

    public IGenerationRunQuery ByPhase(GenerationWorkflowPhase phase)
    {
        _query = _query.Where(r => r.Phase == phase);
        return this;
    }

    public IGenerationRunQuery ByPhaseAndStatus(GenerationWorkflowPhase phase, GenerationRunStatus status)
    {
        _query = _query.Where(r => r.Phase == phase && r.Status == status);
        return this;
    }

    public IGenerationRunQuery OrderByDateCreated()
    {
        _query = _query.OrderBy(r => r.DateCreated);
        return this;
    }

    public IGenerationRunQuery OrderByDateCreatedDescending()
    {
        _query = _query.OrderByDescending(r => r.DateCreated);
        return this;
    }
}
