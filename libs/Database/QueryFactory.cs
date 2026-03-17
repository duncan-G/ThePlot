using Microsoft.Extensions.DependencyInjection;
using ThePlot.Database.Abstractions;

namespace ThePlot.Database;

public sealed class QueryFactory<TEntity, TQuery>(IServiceProvider serviceProvider) : IQueryFactory<TEntity, TQuery>
    where TQuery : IQuery<TEntity>
{
    public TQuery Create() => ActivatorUtilities.CreateInstance<TQuery>(serviceProvider);
}
