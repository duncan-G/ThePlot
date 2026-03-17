using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ScreenplayImports;

public sealed class ScreenplayImportChunk : IDateStamped
{
    private ScreenplayImportChunk()
    {
    }

    public Guid Id { get; private init; }
    public Guid ScreenplayImportId { get; private init; }
    public int StartPage { get; private init; }
    public int EndPage { get; private init; }
    public ChunkStatus SplitStatus { get; private set; }
    public DateTimeOffset? SplitCompletedAt { get; private set; }
    public string? SplitErrorMessage { get; private set; }
    public ChunkStatus ProcessStatus { get; private set; }
    public DateTimeOffset? ProcessCompletedAt { get; private set; }
    public string? ProcessErrorMessage { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public static ScreenplayImportChunk Create(Guid screenplayImportId, int startPage, int endPage)
    {
        return new ScreenplayImportChunk
        {
            Id = Guid.CreateVersion7(),
            ScreenplayImportId = screenplayImportId,
            StartPage = startPage,
            EndPage = endPage,
            SplitStatus = ChunkStatus.Pending,
            ProcessStatus = ChunkStatus.Pending
        };
    }

    public void SetSplitDone()
    {
        SplitStatus = ChunkStatus.Done;
        SplitCompletedAt = DateTimeOffset.UtcNow;
    }

    public void SetSplitFailed(string message)
    {
        SplitStatus = ChunkStatus.Failed;
        SplitErrorMessage = message;
    }

    public void SetProcessDone()
    {
        ProcessStatus = ChunkStatus.Done;
        ProcessCompletedAt = DateTimeOffset.UtcNow;
    }

    public void SetProcessFailed(string message)
    {
        ProcessStatus = ChunkStatus.Failed;
        ProcessErrorMessage = message;
    }
}
