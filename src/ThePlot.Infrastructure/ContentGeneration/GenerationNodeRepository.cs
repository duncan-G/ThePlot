using ThePlot.Core.ContentGeneration;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class GenerationNodeRepository(PagingTokenHelper pagingTokenHelper)
    : Repository<GenerationNode, Guid>(pagingTokenHelper), IGenerationNodeRepository;
