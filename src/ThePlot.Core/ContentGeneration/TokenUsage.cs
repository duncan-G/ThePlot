namespace ThePlot.Core.ContentGeneration;

public sealed record TokenUsage(
    int InputTextTokens = 0,
    int InputAudioTokens = 0,
    int OutputTextTokens = 0,
    int OutputAudioTokens = 0)
{
    public static readonly TokenUsage None = new();

    public int TotalInputTokens => InputTextTokens + InputAudioTokens;
    public int TotalOutputTokens => OutputTextTokens + OutputAudioTokens;
    public int TotalTokens => TotalInputTokens + TotalOutputTokens;

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(
            a.InputTextTokens + b.InputTextTokens,
            a.InputAudioTokens + b.InputAudioTokens,
            a.OutputTextTokens + b.OutputTextTokens,
            a.OutputAudioTokens + b.OutputAudioTokens);
}
