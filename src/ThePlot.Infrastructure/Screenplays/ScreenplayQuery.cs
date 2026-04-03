using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Screenplays;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Screenplays;

internal sealed class ScreenplayQuery(ThePlotContext context) : IScreenplayQuery, IExecutableQuery<Screenplay>
{
    private IQueryable<Screenplay> _query = context.Screenplays.AsNoTracking();

    IQueryable<Screenplay> IExecutableQuery<Screenplay>.AsQueryable() => _query;

    public IScreenplayQuery ById(Guid id)
    {
        _query = _query.Where(s => s.Id == id);
        return this;
    }
}
