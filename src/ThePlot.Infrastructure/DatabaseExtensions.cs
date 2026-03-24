using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ThePlot.Core.Characters;
using ThePlot.Core.Locations;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ThePlot.Core.Voices;
using ThePlot.Infrastructure.Characters;
using ThePlot.Infrastructure.Locations;
using ThePlot.Infrastructure.SceneElements;
using ThePlot.Infrastructure.Scenes;
using ThePlot.Infrastructure.ScreenplayImports;
using ThePlot.Infrastructure.Screenplays;
using ThePlot.Infrastructure.Voices;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure;

public static class DatabaseExtensions
{
    public static void ConfigureVectorTypes(this NpgsqlDataSourceBuilder builder) =>
        builder.UseVector();

    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        Action<DatabaseOptions> configureOptions)
    {
        services.AddCoreDatabaseServices<ThePlotContext>(configureOptions);

        AddRepositories(services);
        AddQueries(services);
        AddQueryFactories(services);

        return services;
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IScreenplayRepository, ScreenplayRepository>()
            .AddScoped<IScreenplayImportRepository, ScreenplayImportRepository>()
            .AddScoped<IScreenplayImportChunkRepository, ScreenplayImportChunkRepository>()
            .AddScoped<ISceneRepository, SceneRepository>()
            .AddScoped<ISceneElementRepository, SceneElementRepository>()
            .AddScoped<ICharacterRepository, CharacterRepository>()
            .AddScoped<IVoiceRepository, VoiceRepository>()
            .AddScoped<ILocationRepository, LocationRepository>();
    }

    private static void AddQueries(this IServiceCollection services)
    {
        services.AddScoped<IScreenplayQuery, ScreenplayQuery>()
            .AddScoped<IScreenplayImportQuery, ScreenplayImportQuery>()
            .AddScoped<IScreenplayImportChunkQuery, ScreenplayImportChunkQuery>()
            .AddScoped<ISceneQuery, SceneQuery>()
            .AddScoped<ISceneElementQuery, SceneElementQuery>()
            .AddScoped<ICharacterQuery, CharacterQuery>()
            .AddScoped<IVoiceQuery, VoiceQuery>()
            .AddScoped<ILocationQuery, LocationQuery>();
    }

    private static void AddQueryFactories(this IServiceCollection services)
    {
        services.AddScoped<IQueryFactory<Screenplay, IScreenplayQuery>, QueryFactory<Screenplay, ScreenplayQuery>>()
            .AddScoped<IQueryFactory<ScreenplayImport, IScreenplayImportQuery>, QueryFactory<ScreenplayImport, ScreenplayImportQuery>>()
            .AddScoped<IQueryFactory<ScreenplayImportChunk, IScreenplayImportChunkQuery>, QueryFactory<ScreenplayImportChunk, ScreenplayImportChunkQuery>>()
            .AddScoped<IQueryFactory<Scene, ISceneQuery>, QueryFactory<Scene, SceneQuery>>()
            .AddScoped<IQueryFactory<SceneElement, ISceneElementQuery>, QueryFactory<SceneElement, SceneElementQuery>>()
            .AddScoped<IQueryFactory<Character, ICharacterQuery>, QueryFactory<Character, CharacterQuery>>()
            .AddScoped<IQueryFactory<Voice, IVoiceQuery>, QueryFactory<Voice, VoiceQuery>>()
            .AddScoped<IQueryFactory<Location, ILocationQuery>, QueryFactory<Location, LocationQuery>>();
    }
}
