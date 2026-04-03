using Microsoft.EntityFrameworkCore;
using ThePlot.Core.ScreenplayImports;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.ScreenplayImports;

internal sealed class ScreenplayImportQuery(ThePlotContext context) : IScreenplayImportQuery, IExecutableQuery<ScreenplayImport>
{
    private IQueryable<ScreenplayImport> _query = context.ScreenplayImports.AsNoTracking();

    IQueryable<ScreenplayImport> IExecutableQuery<ScreenplayImport>.AsQueryable() => _query;

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
