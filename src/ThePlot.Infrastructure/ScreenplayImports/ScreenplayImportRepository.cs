using ThePlot.Core.ScreenplayImports;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ScreenplayImports;

public sealed class ScreenplayImportRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<ScreenplayImport, Guid>(pagingTokenHelper), IScreenplayImportRepository;
