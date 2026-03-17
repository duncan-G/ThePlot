using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Screenplays;

namespace ThePlot.Infrastructure.Screenplays;

public sealed class ScreenplayQuery(ThePlotContext context) : IScreenplayQuery
{
    private IQueryable<Screenplay> _query = context.Screenplays.AsNoTracking();

    public IQueryable<Screenplay> AsQueryable() => _query;

    public IScreenplayQuery ById(Guid id)
    {
        _query = _query.Where(s => s.Id == id);
        return this;
    }
}
