using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ScreenplayImports;

namespace ThePlot.Infrastructure.ScreenplayImports;

public sealed class ScreenplayImportChunkQuery(ThePlotContext context) : IScreenplayImportChunkQuery
{
    private IQueryable<ScreenplayImportChunk> _query = context.ScreenplayImportChunks.AsNoTracking();

    public IQueryable<ScreenplayImportChunk> AsQueryable() => _query;

    public IScreenplayImportChunkQuery ByScreenplayImportId(Guid screenplayImportId)
    {
        _query = _query.Where(c => c.ScreenplayImportId == screenplayImportId);
        return this;
    }

    public IScreenplayImportChunkQuery ByStartPage(int startPage)
    {
        _query = _query.Where(c => c.StartPage == startPage);
        return this;
    }
}
