namespace ThePlot.Core.ScreenplayImports;

/// <summary>
/// Messages sent to screenplay-import-status queue for the API to persist and notify users.
/// </summary>
public sealed record ScreenplayImportStatusMessage
{
    public required string Kind { get; init; }
    public string? SourceBlobName { get; init; }
    public Guid? ScreenplayId { get; init; }
    public string? Reason { get; init; }
    public int? StartPage { get; init; }
    public int? EndPage { get; init; }
    public int? TotalPages { get; init; }
    public bool IsFirstChunk { get; init; }

    public static ScreenplayImportStatusMessage BlobUploaded(string sourceBlobName) =>
        new() { Kind = "BlobUploaded", SourceBlobName = sourceBlobName };

    public static ScreenplayImportStatusMessage ValidationFailed(string sourceBlobName, string reason) =>
        new() { Kind = "ValidationFailed", SourceBlobName = sourceBlobName, Reason = reason };

    public static ScreenplayImportStatusMessage ValidationPassed(Guid screenplayId, string sourceBlobName) =>
        new() { Kind = "ValidationPassed", ScreenplayId = screenplayId, SourceBlobName = sourceBlobName };

    public static ScreenplayImportStatusMessage ChunkSplitDone(Guid screenplayId, int startPage, int endPage, int totalPages, bool isFirstChunk) =>
        new() { Kind = "ChunkSplitDone", ScreenplayId = screenplayId, StartPage = startPage, EndPage = endPage, TotalPages = totalPages, IsFirstChunk = isFirstChunk };

    public static ScreenplayImportStatusMessage ChunkProcessDone(Guid screenplayId, int startPage) =>
        new() { Kind = "ChunkProcessDone", ScreenplayId = screenplayId, StartPage = startPage };

    public static ScreenplayImportStatusMessage ChunkProcessFailed(Guid screenplayId, int startPage, string reason) =>
        new() { Kind = "ChunkProcessFailed", ScreenplayId = screenplayId, StartPage = startPage, Reason = reason };

    public static ScreenplayImportStatusMessage ImportFailed(Guid? screenplayId, string? sourceBlobName, string reason) =>
        new() { Kind = "ImportFailed", ScreenplayId = screenplayId, SourceBlobName = sourceBlobName, Reason = reason };
}
