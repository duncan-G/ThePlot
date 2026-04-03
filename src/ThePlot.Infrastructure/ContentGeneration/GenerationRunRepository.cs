using ThePlot.Core.ContentGeneration;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class GenerationRunRepository(PagingTokenHelper pagingTokenHelper)
    : Repository<GenerationRun, Guid>(pagingTokenHelper), IGenerationRunRepository;
