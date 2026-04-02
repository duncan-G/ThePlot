namespace ThePlot.Core.ContentGeneration;

public sealed class GenerationAlreadyActiveException(Guid activeRunId, Guid screenplayId)
    : InvalidOperationException(
        $"Screenplay {screenplayId} already has an active generation run {activeRunId}.")
{
    public Guid ActiveRunId { get; } = activeRunId;
    public Guid ScreenplayId { get; } = screenplayId;
}
