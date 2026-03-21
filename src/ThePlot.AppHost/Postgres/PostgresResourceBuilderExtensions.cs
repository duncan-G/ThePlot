namespace ThePlot.AppHost.Postgres;

public static class PostgresResourceBuilderExtensions
{
    private const string InitFilesPath = "PostgresInit";

    public static IResourceBuilder<Aspire.Hosting.Azure.AzurePostgresFlexibleServerDatabaseResource> AddDatabase(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        var postgres = builder.AddAzurePostgresFlexibleServer($"{name}-server");

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

        return postgres.AddDatabase(name);
    }

    public static IResourceBuilder<ProjectResource> WithSchemaBuilder<TProject>(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<Aspire.Hosting.Azure.AzurePostgresFlexibleServerDatabaseResource>  postgresDb,
        [ResourceName] string projectName)
        where TProject : IProjectMetadata, new()
    {
        var schemaBuilderProject = new TProject();
        var schemaBuilder = builder.AddProject<TProject>(projectName)
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

        return schemaBuilder;
    }
}
