using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ScreenplayImports;

public sealed class ScreenplayImport : IDateStamped
{
    private ScreenplayImport()
    {
    }

    public Guid Id { get; private init; }
    public Guid? ScreenplayId { get; private set; }
    public string SourceBlobName { get; private init; } = null!;
    public DateTimeOffset? BlobUploadedAt { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public int? TotalPages { get; private set; }
    public DateTimeOffset? ImportFailedAt { get; private set; }
    public string? ImportErrorMessage { get; private set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }

    public IReadOnlyList<ScreenplayImportChunk>? Chunks { get; private init; }

    public static ScreenplayImport Create(string sourceBlobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceBlobName);

        return new ScreenplayImport
        {
            Id = Guid.CreateVersion7(),
            SourceBlobName = sourceBlobName,
            BlobUploadedAt = DateTimeOffset.UtcNow
        };
    }

    public void SetValidated(Guid screenplayId)
    {
        ScreenplayId = screenplayId;
        ValidatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTotalPages(int totalPages)
    {
        TotalPages = totalPages;
    }

    public void SetImportFailed(string message)
    {
        ImportFailedAt = DateTimeOffset.UtcNow;
        ImportErrorMessage = message;
    }
}
