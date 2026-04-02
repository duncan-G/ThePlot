namespace ThePlot.Infrastructure.Embeddings;

public interface IEmbeddingClient
{
    Task<float[]> GetEmbeddingAsync(string text, int dimensions = 1024, CancellationToken ct = default);
}
