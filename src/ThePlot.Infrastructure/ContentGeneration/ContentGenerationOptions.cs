using ThePlot.Core.ContentGeneration;

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
    /// Number of TTS nodes to claim and dispatch concurrently per worker cycle,
    /// allowing vLLM to batch requests rather than process them one at a time.
    /// </summary>
    public int TtsBatchSize { get; set; } = 24;

    /// <summary>Pricing for the chat/LLM client (cost per 1M tokens). Zero for local models.</summary>
    public ModelPricing ChatPricing { get; set; } = ModelPricing.Zero;

    /// <summary>Pricing for the embedding client (cost per 1M tokens). Zero for local models.</summary>
    public ModelPricing EmbeddingPricing { get; set; } = ModelPricing.Zero;

    /// <summary>Pricing for the TTS client (cost per 1M tokens). Zero for local models.</summary>
    public ModelPricing TtsPricing { get; set; } = ModelPricing.Zero;
}
