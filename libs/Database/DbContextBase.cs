using Microsoft.EntityFrameworkCore;
using ThePlot.Database.Abstractions;

namespace ThePlot.Database;

public abstract class DbContextBase(DbContextOptions options, QueryFilterService queryFilterService, UserContext userContext)
    : DbContext(options), IDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        QueryFilterHelper.AddQueryFilters(modelBuilder, queryFilterService, userContext);
    }
}
