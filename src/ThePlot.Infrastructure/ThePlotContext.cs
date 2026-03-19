using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;
using ThePlot.Core.Locations;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ThePlot.Core.Voices;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure;

public sealed class ThePlotContext(
    DbContextOptions<ThePlotContext> options,
    QueryFilterService queryFilterService,
    UserContext userContext)
    : DbContextBase(options, queryFilterService, userContext)
{
    public DbSet<Screenplay> Screenplays => Set<Screenplay>();
    public DbSet<ScreenplayImport> ScreenplayImports => Set<ScreenplayImport>();
    public DbSet<ScreenplayImportChunk> ScreenplayImportChunks => Set<ScreenplayImportChunk>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<SceneElement> SceneElements => Set<SceneElement>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<Voice> Voices => Set<Voice>();
    public DbSet<Location> Locations => Set<Location>();

    private static readonly DateStampedSaveChangesInterceptor DateStampedInterceptor = new();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("theplot");
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Screenplay>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.DateCreated);
            builder.Property(s => s.DateDeleted);
            builder.HasQueryFilter(s => s.DateDeleted == null);
            builder.Property(s => s.PdfMetadata).HasColumnType("jsonb");
            builder.Property(s => s.Authors).HasColumnType("text[]");
            builder.HasNoScope();
        });

        modelBuilder.Entity<ScreenplayImport>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.ScreenplayId);
            builder.HasIndex(s => s.SourceBlobName);
            builder.HasIndex(s => s.DateCreated);
            builder.HasNoScope();
        });

        modelBuilder.Entity<ScreenplayImportChunk>(builder =>
        {
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.ScreenplayImportId);
            builder.HasIndex(c => new { c.ScreenplayImportId, c.StartPage }).IsUnique();
            builder.HasOne<ScreenplayImport>()
                .WithMany(si => si.Chunks)
                .HasForeignKey(c => c.ScreenplayImportId)
                .IsRequired();
            builder.Property(c => c.SplitStatus).HasConversion<string>();
            builder.Property(c => c.ProcessStatus).HasConversion<string>();
            builder.HasNoScope();
        });

        modelBuilder.Entity<Scene>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.ScreenplayId);
            builder.HasIndex(s => s.DateCreated);
            builder.HasOne(s => s.Screenplay)
                .WithMany(p => p.Scenes)
                .HasForeignKey(s => s.ScreenplayId)
                .IsRequired(false);
            builder.HasQueryFilter(s => s.Screenplay != null && s.Screenplay.DateDeleted == null);
            builder.HasOne(s => s.Location).WithMany().HasForeignKey(s => s.LocationId);
            builder.Property(s => s.InteriorExterior).HasConversion(LocationTypeConverter.Instance);
            builder.Property(s => s.Embedding).HasColumnType("vector(1536)");
            builder.Property(s => s.PdfMetadata).HasColumnType("jsonb");
            builder.HasNoScope();
        });

        modelBuilder.Entity<SceneElement>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.SceneId);
            builder.HasIndex(s => s.DateCreated);
            builder.HasOne(s => s.Scene)
                .WithMany(s => s.SceneElements)
                .HasForeignKey(s => s.SceneId);
            builder.HasQueryFilter(s => s.Scene != null && s.Scene.Screenplay != null && s.Scene.Screenplay.DateDeleted == null);
            builder.HasOne(s => s.Character).WithMany().HasForeignKey(s => s.CharacterId);
            builder.Property(s => s.Type).HasConversion(
                v => v.ToString(),
                v => Enum.Parse<SceneElementType>(v, true));
            builder.Property(s => s.Embedding).HasColumnType("vector(1536)");
            builder.Property(s => s.PdfMetadata).HasColumnType("jsonb");
            builder.Property(s => s.Content).HasColumnType("jsonb");
            builder.HasNoScope();
        });

        modelBuilder.Entity<Character>(builder =>
        {
            builder.HasKey(c => c.Id);
            builder.HasOne(c => c.Voice).WithMany().HasForeignKey(c => c.VoiceId);
            builder.Property(c => c.Aliases).HasColumnType("text[]");
            builder.HasNoScope();
        });

        modelBuilder.Entity<Voice>(builder =>
        {
            builder.HasKey(v => v.Id);
            builder.HasNoScope();
        });

        modelBuilder.Entity<Location>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.HasNoScope();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(DateStampedInterceptor);
}
