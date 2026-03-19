namespace ThePlot.AppHost.Postgres;

public static class PostgresResourceBuilderExtensions
{
    private const string DatabaseName = "theplot-db";
    private const string InitFilesPath = "PostgresInit";

    /// <summary>
    /// Adds ThePlot PostgreSQL database and schema builder. Uses container (pgvector) for dev, Azure Flexible Server for prod.
    /// PgVector is enabled via WithPgVector() for both container (image + init script) and Azure (azure.extensions).
    /// </summary>
    public static (IResourceBuilder<Aspire.Hosting.Azure.AzurePostgresFlexibleServerResource> Server, IResourceBuilder<Aspire.Hosting.Azure.AzurePostgresFlexibleServerDatabaseResource> Database, IResourceBuilder<ProjectResource> SchemaBuilder) AddDatabase(
        this IDistributedApplicationBuilder builder)
    {
        var postgres = builder.AddAzurePostgresFlexibleServer("postgres");

        if (!builder.ExecutionContext.IsPublishMode)
        {
            postgres = postgres.RunAsContainer(container =>
            {
                container
                    .WithPgVector(InitFilesPath)
                    .WithVolume("theplot-volume", "/var/lib/postgresql")
                    .WithPgAdmin();
            });
        }
        else
        {
            postgres = postgres.WithPgVector();
        }

        var postgresDb = postgres.AddDatabase(DatabaseName);

        var schemaBuilderProject = new Projects.ThePlot_SchemaBuilder();
        var schemaBuilder = builder.AddProject<Projects.ThePlot_SchemaBuilder>("theplot-schema-builder")
            .WithReference(postgresDb)
            .WaitFor(postgresDb);

        if (!builder.ExecutionContext.IsPublishMode)
        {
            var schemaBuilderDir = Path.GetDirectoryName(schemaBuilderProject.ProjectPath)!;
            schemaBuilder = schemaBuilder.WithCommand(
                "rebuild-schema",
                "Rebuild",
                context => SchemaBuilderCommands.ExecuteRebuildSchemaAsync(context, schemaBuilderDir, postgresDb.Resource),
                new CommandOptions
                {
                    IconName = "ArrowClockwise",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true
                });
        }

        return (postgres, postgresDb, schemaBuilder);
    }
}
