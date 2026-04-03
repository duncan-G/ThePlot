using ThePlot.Core.ContentGeneration;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class GenerationAttemptRepository(PagingTokenHelper pagingTokenHelper)
    : Repository<GenerationAttempt, Guid>(pagingTokenHelper), IGenerationAttemptRepository;
