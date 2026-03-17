using ThePlot.Core.ScreenplayImports;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ScreenplayImports;

public sealed class ScreenplayImportChunkRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<ScreenplayImportChunk, Guid>(pagingTokenHelper), IScreenplayImportChunkRepository;
