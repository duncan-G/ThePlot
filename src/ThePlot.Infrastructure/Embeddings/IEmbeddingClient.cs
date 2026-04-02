namespace ThePlot.Infrastructure.Embeddings;

public interface IEmbeddingClient
{
    Task<float[]> GetEmbeddingAsync(string text, int? dimensions = null, CancellationToken ct = default);
}
