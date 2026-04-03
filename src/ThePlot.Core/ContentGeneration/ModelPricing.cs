namespace ThePlot.Core.ContentGeneration;

/// <summary>
/// Cost per 1 million tokens for each token category.
/// </summary>
public sealed record ModelPricing(
    decimal InputTextTokenCostPer1M = 0,
    decimal InputAudioTokenCostPer1M = 0,
    decimal OutputTextTokenCostPer1M = 0,
    decimal OutputAudioTokenCostPer1M = 0)
{
    public static readonly ModelPricing Zero = new();

    public decimal CalculateCost(TokenUsage usage) =>
        (usage.InputTextTokens * InputTextTokenCostPer1M
         + usage.InputAudioTokens * InputAudioTokenCostPer1M
         + usage.OutputTextTokens * OutputTextTokenCostPer1M
         + usage.OutputAudioTokens * OutputAudioTokenCostPer1M) / 1_000_000m;
}
