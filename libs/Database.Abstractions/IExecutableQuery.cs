namespace ThePlot.Database.Abstractions;

internal interface IExecutableQuery<out T> : IQuery<T>
{
    IQueryable<T> AsQueryable();
}
