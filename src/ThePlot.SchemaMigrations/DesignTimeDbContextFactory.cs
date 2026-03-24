using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ThePlot.Database;
using ThePlot.Database.Abstractions;
using ThePlot.Infrastructure;

namespace ThePlot.SchemaMigrations;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ThePlotContext>
{
    public ThePlotContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThePlotContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=theplot-db", o => o.UseVector())
            .UseSnakeCaseNamingConvention();

        return new ThePlotContext(optionsBuilder.Options, new QueryFilterService(), new UserContext());
    }
}
