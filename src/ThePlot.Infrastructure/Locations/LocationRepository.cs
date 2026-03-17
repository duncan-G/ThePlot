using ThePlot.Core.Locations;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Locations;

public sealed class LocationRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<Location, Guid>(pagingTokenHelper), ILocationRepository;
