using ThePlot.Core.ContentGeneration;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class GenerationEdgeRepository(PagingTokenHelper pagingTokenHelper)
    : Repository<GenerationEdge, Guid>(pagingTokenHelper), IGenerationEdgeRepository;
