using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ScreenplayImports;

internal sealed class ScreenplayImportChunkQuery(ThePlotContext context) : IScreenplayImportChunkQuery, IExecutableQuery<ScreenplayImportChunk>
{
    private IQueryable<ScreenplayImportChunk> _query = context.ScreenplayImportChunks.AsNoTracking();

    IQueryable<ScreenplayImportChunk> IExecutableQuery<ScreenplayImportChunk>.AsQueryable() => _query;

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
