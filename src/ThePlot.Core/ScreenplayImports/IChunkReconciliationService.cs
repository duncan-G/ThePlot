namespace ThePlot.Core.ScreenplayImports;

public interface IChunkReconciliationService
{
    Task ReconcileAsync(Guid screenplayId, CancellationToken ct);
}
