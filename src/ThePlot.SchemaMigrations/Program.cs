using Microsoft.EntityFrameworkCore;
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