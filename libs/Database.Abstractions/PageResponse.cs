namespace ThePlot.Database.Abstractions;

public sealed record PageResponse<TEntity>(
    IReadOnlyList<TEntity> Items,
    string? NextPageToken);
