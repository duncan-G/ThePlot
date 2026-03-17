using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ScreenplayImports;

public interface IScreenplayImportChunkQuery : IQuery<ScreenplayImportChunk>
{
    IScreenplayImportChunkQuery ByScreenplayImportId(Guid screenplayImportId);
    IScreenplayImportChunkQuery ByStartPage(int startPage);
}
