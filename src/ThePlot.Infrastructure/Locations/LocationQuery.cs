using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Locations;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Locations;

internal sealed class LocationQuery(ThePlotContext context) : ILocationQuery, IExecutableQuery<Location>
{
    private IQueryable<Location> _query = context.Locations.AsNoTracking();

    IQueryable<Location> IExecutableQuery<Location>.AsQueryable() => _query;

    public ILocationQuery ByIds(IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        _query = _query.Where(l => list.Contains(l.Id));
        return this;
    }
}
