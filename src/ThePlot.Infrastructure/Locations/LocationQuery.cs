using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Locations;

namespace ThePlot.Infrastructure.Locations;

public sealed class LocationQuery(ThePlotContext context) : ILocationQuery
{
    private IQueryable<Location> _query = context.Locations.AsNoTracking();

    public IQueryable<Location> AsQueryable() => _query;

    public ILocationQuery ByIds(IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        _query = _query.Where(l => list.Contains(l.Id));
        return this;
    }
}
