using ThePlot.Core.ContentGeneration;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class GeneratedArtifactRepository(PagingTokenHelper pagingTokenHelper)
    : Repository<GeneratedArtifact, Guid>(pagingTokenHelper), IGeneratedArtifactRepository;
