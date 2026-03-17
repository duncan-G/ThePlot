using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ScreenplayImports;

public interface IScreenplayImportQuery : IQuery<ScreenplayImport>
{
    IScreenplayImportQuery ByScreenplayId(Guid screenplayId);
    IScreenplayImportQuery BySourceBlobName(string sourceBlobName);
}
