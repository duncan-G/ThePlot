using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using ThePlot.Database.Abstractions;

namespace ThePlot.Database;

public abstract class Repository<TEntity, TKey>(PagingTokenHelper pagingTokenHelper) : IRepository<TEntity, TKey>
    where TEntity : class
{
    private static string? PrimaryKeyName => ((DbContextBase)UnitOfWork.DbContext)
        .Model
        .FindEntityType(typeof(TEntity))
        ?.FindPrimaryKey()?.Properties[0].Name;

    private static DbSet<TEntity> DbSet => ((DbContextBase)UnitOfWork.DbContext).Set<TEntity>();

    private static IUnitOfWork UnitOfWork => UnitOfWorkContext.Current ??
                                             throw new InvalidOperationException(
                                                 "No active unit of work found in the current context.");


    public virtual async Task<bool> ExistsByKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(ExistsByKeyAsync)}", ActivityKind.Server);
        activity?.AddTag("Key", key);
        if (PrimaryKeyName == null)
        {
            throw new InvalidOperationException("Entity does not have a primary key.");
        }

        // Build the lambda: e => EF.Property<TKey>(e, keyPropertyName) == id
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MethodCallExpression propertyExpression = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(TKey)],
            parameter,
            Expression.Constant(PrimaryKeyName));

        ConstantExpression idExpression = Expression.Constant(key, typeof(TKey));
        BinaryExpression equality = Expression.Equal(propertyExpression, idExpression);
        Expression<Func<TEntity, bool>> lambda = Expression.Lambda<Func<TEntity, bool>>(equality, parameter);

        return await DbSet.AnyAsync(lambda, cancellationToken);
    }

    public Task<bool> ExistsByQueryAsync(IQuery<TEntity> query, CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(ExistsByQueryAsync)}",
                ActivityKind.Server);

        return query.AsQueryable().AnyAsync(cancellationToken);
    }

    public virtual async Task<TEntity?> GetByKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(GetByKeyAsync)}", ActivityKind.Server);
        activity?.AddTag("Key", key);

        return await DbSet.FindAsync([key], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetByQueryAsync(IQuery<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(GetByQueryAsync)}", ActivityKind.Server);

        return await query.AsQueryable().ToListAsync(cancellationToken);
    }

    public virtual async Task<PageResponse<TEntity>> GetByQueryPagedAsync<TSort>(
        IQuery<TEntity> query,
        PagingToken<TEntity, TSort, TKey> pagingToken,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(GetByQueryPagedAsync)}",
                ActivityKind.Server);
        activity?.AddTag("PagingToken", pagingToken.ToString());
        IQueryable<TEntity> queryable = query.AsQueryable();

        if (pagingToken.Cursor is { } pagingCursor)
        {
            // Build an expression equivalent to:
            // e => (e.Order < LastOrderValue) || (e.Order == LastOrderValue && e.Pk < LastPkValue)
            ParameterExpression param = Expression.Parameter(typeof(TEntity), "e");

            InvocationExpression sortKeyBody = Expression.Invoke(pagingToken.SortKeySelector, param);
            InvocationExpression primaryKeyBody = Expression.Invoke(pagingToken.PrimaryKeySelector, param);

            ConstantExpression lastSortKeyValueConst =
                Expression.Constant(pagingCursor.LastSortKeyValue, typeof(TSort));
            ConstantExpression lastPrimaryKeyValueConst =
                Expression.Constant(pagingCursor.LastPrimaryKeyValue, typeof(TKey));

            // e.SortKey < LastSortKeyValue
            BinaryExpression sortKeyLessThan = Expression.LessThan(sortKeyBody, lastSortKeyValueConst);
            // e.SortKey == LastSortKeyValue
            BinaryExpression sortKeyEqual = Expression.Equal(sortKeyBody, lastSortKeyValueConst);
            // e.PrimaryKey < LastPkValue
            BinaryExpression primaryKeyLessThan = Expression.LessThan(primaryKeyBody, lastPrimaryKeyValueConst);

            // (e.Order == LastOrderValue && e.Pk < LastPkValue)
            BinaryExpression combinedSubPredicate = Expression.AndAlso(sortKeyEqual, primaryKeyLessThan);
            // (e.Order < LastOrderValue) || (combinedSubPredicate)
            BinaryExpression compositePredicate = Expression.OrElse(sortKeyLessThan, combinedSubPredicate);

            Expression<Func<TEntity, bool>> whereLambda =
                Expression.Lambda<Func<TEntity, bool>>(compositePredicate, param);

            queryable = queryable.Where(whereLambda);
        }

        queryable = queryable
            .OrderByDescending(pagingToken.SortKeySelector)
            .ThenByDescending(pagingToken.PrimaryKeySelector);

        List<TEntity> itemsPlusOne = await queryable
            .Take(pagingToken.PageSize + 1)
            .ToListAsync(cancellationToken);

        bool hasMore = itemsPlusOne.Count > pagingToken.PageSize;
        List<TEntity> items = hasMore ? itemsPlusOne.Take(pagingToken.PageSize).ToList() : itemsPlusOne;

        string? nextPageToken = null;
        if (hasMore)
        {
            TEntity lastItem = items.Last();
            TSort lastSortKeyValue = pagingToken.SortKeySelector.Compile().Invoke(lastItem);
            TKey lastPrimaryKeyValue = pagingToken.PrimaryKeySelector.Compile().Invoke(lastItem);
            nextPageToken = pagingTokenHelper.Encode(pagingToken with
            {
                Cursor = new PagingCursor<TSort, TKey>(lastSortKeyValue, lastPrimaryKeyValue)
            });
        }

        return new PageResponse<TEntity>(items, nextPageToken);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await VerifyReadWriteUnitOfWork();

        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(AddAsync)}", ActivityKind.Server);
        activity?.AddTag("Key", GetPrimaryKeyValue(entity));

        await DbSet.AddAsync(entity, cancellationToken);
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await VerifyReadWriteUnitOfWork();

        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(UpdateAsync)}", ActivityKind.Server);
        activity?.AddTag("Key", GetPrimaryKeyValue(entity));

        DbSet.Entry(entity).State = EntityState.Modified;
    }

    public virtual async Task<int> UpdateByQueryAsync(IQuery<TEntity> query,
        Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
        CancellationToken cancellationToken = default)
    {
        await VerifyReadWriteUnitOfWork();

        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(UpdateByQueryAsync)}",
                ActivityKind.Server);

        return await query.AsQueryable().ExecuteUpdateAsync(setPropertyCalls, cancellationToken);
    }

    public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await VerifyReadWriteUnitOfWork();

        using Activity? activity =
            Activity.Current?.Source.StartActivity($"{GetType().Name}.{nameof(RemoveAsync)}", ActivityKind.Server);
        activity?.AddTag("Key", GetPrimaryKeyValue(entity));

        DbSet.Remove(entity);
    }

    private async Task VerifyReadWriteUnitOfWork()
    {
        if (UnitOfWork is ReadOnlyUnitOfWork)
        {
            throw new InvalidOperationException("Cannot use ReadOnly unit of work when writing to the database.");
        }

        if (UnitOfWork is ReadWriteUnitOfWork readWriteUnitOfWork)
        {
            await readWriteUnitOfWork.EnsureTransactionAsync();
        }
    }

    private object? GetPrimaryKeyValue(TEntity entity) =>
        PrimaryKeyName == null
            ? null
            : DbSet.Entry(entity).Property(PrimaryKeyName).CurrentValue;
}
