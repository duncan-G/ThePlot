using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThePlot.Infrastructure;
using ThePlot.Database;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration["ConnectionStrings:theplot-db"]
            ?? context.Configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException(
                "Connection string not configured. Set ConnectionStrings__theplot-db (Aspire) or Database:ConnectionString (appsettings).");
        var commandTimeout = context.Configuration.GetValue("Database:CommandTimeout", 30);

        services.AddCoreDatabaseServices<ThePlotContext>(options =>
        {
            context.Configuration.GetSection("Database").Bind(options);
            if (!string.IsNullOrEmpty(connectionString))
            {
                typeof(ThePlot.Database.DatabaseOptions).GetProperty("ConnectionString")!
                    .SetValue(options, connectionString);
            }
            if (commandTimeout > 0)
            {
                typeof(ThePlot.Database.DatabaseOptions).GetProperty("CommandTimeout")!
                    .SetValue(options, commandTimeout);
            }
        });
    })
    .Build();

var rebuild = args.Contains("--rebuild-schema", StringComparer.OrdinalIgnoreCase);

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ThePlotContext>();
    var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();

    if (!await databaseCreator.ExistsAsync())
    {
        Console.WriteLine("Creating database...");
        await databaseCreator.CreateAsync();
    }

    if (rebuild)
    {
        var schema = dbContext.Model.GetDefaultSchema();
        Console.WriteLine($"Rebuild requested. Dropping and recreating schema: {schema}...");

#pragma warning disable EF1002
        await dbContext.Database.ExecuteSqlRawAsync($@"
            DROP SCHEMA IF EXISTS ""{schema}"" CASCADE;
            CREATE SCHEMA ""{schema}"";");
#pragma warning restore EF1002

        await databaseCreator.CreateTablesAsync();
    }
    
    await dbContext.Database.EnsureCreatedAsync();


    Console.WriteLine("Schema creation/update complete.");
}
catch (Exception ex)
{
    Console.WriteLine("Schema creation failed: " + ex.Message);
    return 1;
}
return 0;