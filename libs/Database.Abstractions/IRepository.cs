namespace ThePlot.Database.Abstractions;

public interface IRepository<TEntity, TKey> where TEntity : class
{
    Task<bool> ExistsByKeyAsync(TKey key, CancellationToken cancellationToken = default);

    Task<bool> ExistsByQueryAsync(IQuery<TEntity> query, CancellationToken cancellationToken = default);

    Task<TEntity?> GetByKeyAsync(TKey key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetByQueryAsync(IQuery<TEntity> query,
        CancellationToken cancellationToken = default);

    Task<PageResponse<TEntity>> GetByQueryPagedAsync<TSort>(
        IQuery<TEntity> query,
        PagingToken<TEntity, TSort, TKey> pagingToken,
        CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<int> UpdateByQueryAsync(
        IQuery<TEntity> query,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TEntity>> setPropertyCalls,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);
}
