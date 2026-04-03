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
                    .WithVolume("theplot-db", "/var/lib/postgresql")
                    .WithPgAdmin();
            });
        }
        else
        {
            postgres = postgres.WithPgVector();
        }

        return postgres.AddDatabase(name);
    }

    public static IResourceBuilder<ProjectResource> WithSchemaMigrations<TProject>(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<Aspire.Hosting.Azure.AzurePostgresFlexibleServerDatabaseResource>  postgresDb,
        [ResourceName] string projectName)
        where TProject : IProjectMetadata, new()
    {
        var schemaMigrationsProject = new TProject();
        var schemaMigrations = builder.AddProject<TProject>(projectName)
            .WithReference(postgresDb)
            .WaitFor(postgresDb);

        if (builder.ExecutionContext.IsPublishMode)
        {
#pragma warning disable ASPIREAZURE002 // PublishAsAzureContainerAppJob is experimental
            schemaMigrations.PublishAsAzureContainerAppJob();
#pragma warning restore ASPIREAZURE002
        }
        else
        {
            var schemaMigrationsDir = Path.GetDirectoryName(schemaMigrationsProject.ProjectPath)!;
            schemaMigrations = schemaMigrations.WithCommand(
                "rebuild-schema",
                "Rebuild",
                context => SchemaMigrationsCommands.ExecuteRebuildSchemaAsync(context, schemaMigrationsDir, postgresDb.Resource),
                new CommandOptions
                {
                    IconName = "ArrowClockwise",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true
                });
        }

        return schemaMigrations;
    }
}
