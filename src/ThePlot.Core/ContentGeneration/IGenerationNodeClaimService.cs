namespace ThePlot.Core.ContentGeneration;

public interface IGenerationNodeClaimService
{
    Task<ClaimedGenerationWork?> TryClaimNextAsync(string workerId, CancellationToken ct);
}
