namespace ThePlot.Infrastructure.ContentGeneration;

public sealed class ContentGenerationOptions
{
    public const string SectionName = "ContentGeneration";

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetryAttemptsPerNode { get; set; } = 5;
    public TimeSpan WorkerIdleDelay { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>Exponential backoff base for NeedsRetry (2^retry * base).</summary>
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum pgvector cosine distance (0 = identical, 2 = opposite) below which an
    /// existing voice is considered a match and reused instead of generating a new one.
    /// </summary>
    public double VoiceSimilarityThreshold { get; set; } = 0.15;
}
