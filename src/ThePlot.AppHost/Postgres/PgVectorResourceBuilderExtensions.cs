using Azure.Provisioning.PostgreSql;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace ThePlot.AppHost.Postgres;

/// <summary>
/// Extension methods for enabling the PgVector extension on PostgreSQL resources.
/// See: https://github.com/dotnet/aspire/issues/3052
/// </summary>
public static class PgVectorResourceBuilderExtensions
{
    private const string PgVectorImage = "pgvector/pgvector";
    private const string PgVectorTag = "pg18";
    private const string DefaultInitFilesPath = "PostgresInit";

    /// <summary>
    /// Configures a PostgreSQL container to use the PgVector image and init script.
    /// Use when running Postgres as a container (e.g. via RunAsContainer).
    /// The init script must create the database and run <c>CREATE EXTENSION IF NOT EXISTS vector;</c>.
    /// </summary>
    /// <param name="builder">The PostgreSQL server resource builder.</param>
    /// <param name="initFilesPath">Path to the init folder containing SQL scripts (default: PostgresInit).</param>
    public static IResourceBuilder<PostgresServerResource> WithPgVector(
        this IResourceBuilder<PostgresServerResource> builder,
        string initFilesPath = DefaultInitFilesPath)
    {
        return builder
            .WithImage(PgVectorImage, PgVectorTag)
            .WithInitFiles(initFilesPath);
    }

    /// <summary>
    /// Configures Azure PostgreSQL Flexible Server to allow the pgvector extension.
    /// Use when deploying to Azure. Enables the vector extension via azure.extensions server parameter.
    /// After deployment, run <c>CREATE EXTENSION IF NOT EXISTS vector;</c> in each database (e.g. via migrations).
    /// </summary>
    /// <param name="builder">The Azure PostgreSQL Flexible Server resource builder.</param>
    public static IResourceBuilder<AzurePostgresFlexibleServerResource> WithPgVector(
        this IResourceBuilder<AzurePostgresFlexibleServerResource> builder)
    {
        builder.ConfigureInfrastructure(infra =>
        {
            var pgServer = infra.GetProvisionableResources()
                .OfType<PostgreSqlFlexibleServer>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Could not find PostgreSqlFlexibleServer in provisionable resources. " +
                    "Ensure ConfigureInfrastructure is called on an Azure PostgreSQL Flexible Server resource.");

            var config = new PostgreSqlFlexibleServerConfiguration(
                "pgvectorExtensions",
                PostgreSqlFlexibleServerConfiguration.ResourceVersions.V2024_08_01)
            {
                Parent = pgServer,
                Name = "azure.extensions",
                Value = "vector",
                Source = "user-override"
            };

            infra.Add(config);
        });

        return builder;
    }
}
