using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThePlot.Infrastructure;
using ThePlot.Database;

var builder = Host.CreateApplicationBuilder(args);

builder.AddAzureNpgsqlDataSource("theplot-db", configureDataSourceBuilder: dsb => dsb.ConfigureVectorTypes());
builder.Services.AddCoreDatabaseServices<ThePlotContext>(options =>
{
    builder.Configuration.GetSection("Database").Bind(options);
});

using var host = builder.Build();

var rebuild = args.Contains("--rebuild-schema", StringComparer.OrdinalIgnoreCase);

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ThePlotContext>();

    if (rebuild)
    {
        var schema = dbContext.Model.GetDefaultSchema();
        Console.WriteLine($"Rebuild requested. Dropping schema: {schema}...");

#pragma warning disable EF1002
        await dbContext.Database.ExecuteSqlRawAsync($@"
            DROP SCHEMA IF EXISTS ""{schema}"" CASCADE;");
#pragma warning restore EF1002
    }

    Console.WriteLine("Applying migrations...");
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("Migrations applied successfully.");
}
catch (Exception ex)
{
    Console.WriteLine("Migration failed: " + ex.Message);
    return 1;
}
return 0;