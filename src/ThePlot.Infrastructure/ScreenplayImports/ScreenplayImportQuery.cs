using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ScreenplayImports;

namespace ThePlot.Infrastructure.ScreenplayImports;

public sealed class ScreenplayImportQuery(ThePlotContext context) : IScreenplayImportQuery
{
    private IQueryable<ScreenplayImport> _query = context.ScreenplayImports.AsNoTracking();

    public IQueryable<ScreenplayImport> AsQueryable() => _query;

    public IScreenplayImportQuery ByScreenplayId(Guid screenplayId)
    {
        _query = _query.Where(s => s.ScreenplayId == screenplayId);
        return this;
    }

    public IScreenplayImportQuery BySourceBlobName(string sourceBlobName)
    {
        _query = _query.Where(s => s.SourceBlobName == sourceBlobName);
        return this;
    }
}
