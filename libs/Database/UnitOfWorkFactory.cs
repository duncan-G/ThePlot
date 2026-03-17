using System.Diagnostics;
using ThePlot.Database.Abstractions;

namespace ThePlot.Database;

public sealed class UnitOfWorkFactory(IDbContext dbContext) : IUnitOfWorkFactory
{
    private static readonly ActivitySource ActivitySource = new("Ultra.UnitOfWork");

    public IUnitOfWork CreateReadOnly(string operationName)
    {
        VerifyNestedUnitOfWork();

        Activity? activity = ActivitySource.StartActivity($"Start UnitOfWork: {operationName}");
        ReadOnlyUnitOfWork unitOfWork = new(dbContext, activity);
        return unitOfWork;
    }

    public IUnitOfWork CreateReadWrite(string operationName)
    {
        VerifyNestedUnitOfWork();

        Activity? activity = ActivitySource.StartActivity($"Start UnitOfWork: {operationName}");
        ReadWriteUnitOfWork unitOfWork = new(dbContext, activity);
        return unitOfWork;
    }

    private void VerifyNestedUnitOfWork()
    {
        if (UnitOfWorkContext.Current != null)
        {
            throw new InvalidOperationException(
                "A unit of work has already been created. Nesting unit of works is currently not supported.");
        }
    }
}
