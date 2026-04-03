using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ContentGeneration;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

internal sealed class GenerationAttemptQuery(ThePlotContext context) : IGenerationAttemptQuery, IExecutableQuery<GenerationAttempt>
{
    private IQueryable<GenerationAttempt> _query = context.GenerationAttempts.AsNoTracking();

    IQueryable<GenerationAttempt> IExecutableQuery<GenerationAttempt>.AsQueryable() => _query;

    public IGenerationAttemptQuery ByNodeId(Guid nodeId)
    {
        _query = _query.Where(a => a.GenerationNodeId == nodeId);
        return this;
    }
}
