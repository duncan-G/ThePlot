using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ThePlot.Core.Characters;
using ThePlot.Core.ContentGeneration;
using ThePlot.Core.Locations;
using ThePlot.Core.SceneElements;
using ThePlot.Core.Scenes;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Core.Screenplays;
using ThePlot.Core.Voices;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure;

internal sealed class ThePlotContext(
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
    public DbSet<GenerationRun> GenerationRuns => Set<GenerationRun>();
    public DbSet<GenerationNode> GenerationNodes => Set<GenerationNode>();
    public DbSet<GenerationEdge> GenerationEdges => Set<GenerationEdge>();
    public DbSet<GenerationAttempt> GenerationAttempts => Set<GenerationAttempt>();
    public DbSet<GeneratedArtifact> GeneratedArtifacts => Set<GeneratedArtifact>();

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
            builder.HasMany(s => s.Voices).WithOne(v => v.Screenplay).HasForeignKey(v => v.ScreenplayId);
            builder.HasMany(s => s.GenerationRuns).WithOne(gr => gr.Screenplay).HasForeignKey(gr => gr.ScreenplayId);
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
            builder.HasIndex(v => new { v.ScreenplayId, v.NameNormalized }).IsUnique();
            builder.HasIndex(v => v.ScreenplayId);
            builder.Property(v => v.Role).HasConversion<string>();
            builder.Property(v => v.AudioMetadata).HasColumnType("jsonb");
            builder.Property(v => v.Embedding).HasColumnType("vector(1024)");
            builder.HasQueryFilter(v => v.Screenplay != null && v.Screenplay.DateDeleted == null);
            builder.HasNoScope();
        });

        modelBuilder.Entity<Location>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.HasNoScope();
        });

        modelBuilder.Entity<GenerationRun>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.HasIndex(r => r.ScreenplayId);
            builder.HasIndex(r => new { r.ScreenplayId, r.Status });
            var phaseConverter = new ValueConverter<GenerationWorkflowPhase, string>(
                v => v.ToString(),
                v => v == "SpeakerDetermination"
                    ? GenerationWorkflowPhase.CharacterResolution
                    : Enum.Parse<GenerationWorkflowPhase>(v));
            builder.Property(r => r.Phase).HasConversion(phaseConverter);
            builder.Property(r => r.Status).HasConversion<string>();
            builder.HasQueryFilter(r => r.Screenplay != null && r.Screenplay.DateDeleted == null);
            builder.HasMany(r => r.Nodes).WithOne(n => n.GenerationRun).HasForeignKey(n => n.GenerationRunId);
            builder.HasNoScope();
        });

        modelBuilder.Entity<GenerationNode>(builder =>
        {
            builder.HasKey(n => n.Id);
            builder.HasIndex(n => n.GenerationRunId);
            builder.HasIndex(n => new { n.GenerationRunId, n.Status, n.RunnableAfterUtc });
            builder.Property(n => n.Kind).HasConversion<string>();
            builder.Property(n => n.Status).HasConversion<string>();
            builder.Property(n => n.Payload).HasColumnType("jsonb");
            builder.Property(n => n.AnalysisResult).HasColumnType("jsonb");
            builder.HasQueryFilter(n => n.GenerationRun != null
                && n.GenerationRun.Screenplay != null
                && n.GenerationRun.Screenplay.DateDeleted == null);
            builder.HasOne(n => n.CurrentAttempt).WithMany().HasForeignKey(n => n.CurrentAttemptId);
            builder.HasMany(n => n.Attempts).WithOne(a => a.GenerationNode).HasForeignKey(a => a.GenerationNodeId);
            builder.HasMany(n => n.Artifacts).WithOne(a => a.GenerationNode).HasForeignKey(a => a.GenerationNodeId);
            builder.HasMany(n => n.OutgoingEdges).WithOne(e => e.FromNode).HasForeignKey(e => e.FromNodeId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(n => n.IncomingEdges).WithOne(e => e.ToNode).HasForeignKey(e => e.ToNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasNoScope();
        });

        modelBuilder.Entity<GenerationEdge>(builder =>
        {
            builder.HasKey(e => new { e.FromNodeId, e.ToNodeId });
            builder.HasIndex(e => e.ToNodeId);
            builder.HasQueryFilter(e => e.FromNode != null
                && e.FromNode.GenerationRun != null
                && e.FromNode.GenerationRun.Screenplay != null
                && e.FromNode.GenerationRun.Screenplay.DateDeleted == null);
            builder.HasNoScope();
        });

        modelBuilder.Entity<GenerationAttempt>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => a.GenerationNodeId);
            builder.HasIndex(a => new { a.GenerationNodeId, a.AttemptNumber }).IsUnique();
            builder.Property(a => a.Status).HasConversion<string>();
            builder.Property(a => a.ProviderRequestJson).HasColumnType("jsonb");
            builder.Property(a => a.ProviderResponseJson).HasColumnType("jsonb");
            builder.Property(a => a.InputTextTokens).HasDefaultValue(0);
            builder.Property(a => a.InputAudioTokens).HasDefaultValue(0);
            builder.Property(a => a.OutputTextTokens).HasDefaultValue(0);
            builder.Property(a => a.OutputAudioTokens).HasDefaultValue(0);
            builder.Property(a => a.Cost).HasDefaultValue(0m);
            builder.HasQueryFilter(a => a.GenerationNode != null
                && a.GenerationNode.GenerationRun != null
                && a.GenerationNode.GenerationRun.Screenplay != null
                && a.GenerationNode.GenerationRun.Screenplay.DateDeleted == null);
            builder.HasMany(a => a.Artifacts).WithOne(ar => ar.GenerationAttempt).HasForeignKey(ar => ar.GenerationAttemptId);
            builder.HasNoScope();
        });

        modelBuilder.Entity<GeneratedArtifact>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => new { a.GenerationNodeId, a.IsCurrent });
            builder.Property(a => a.Metadata).HasColumnType("jsonb");
            builder.HasQueryFilter(a => a.GenerationNode != null
                && a.GenerationNode.GenerationRun != null
                && a.GenerationNode.GenerationRun.Screenplay != null
                && a.GenerationNode.GenerationRun.Screenplay.DateDeleted == null);
            builder.HasNoScope();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(DateStampedInterceptor);
}
