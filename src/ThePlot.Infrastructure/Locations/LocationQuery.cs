using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Locations;

namespace ThePlot.Infrastructure.Locations;

public sealed class LocationQuery(ThePlotContext context) : ILocationQuery
{
    private IQueryable<Location> _query = context.Locations.AsNoTracking();

    public IQueryable<Location> AsQueryable() => _query;
}
