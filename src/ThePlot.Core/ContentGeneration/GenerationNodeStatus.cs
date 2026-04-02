namespace ThePlot.Core.ContentGeneration;

public enum GenerationNodeStatus
{
    Pending,
    Ready,
    Running,
    Succeeded,
    Failed,
    Blocked,
    Cancelled,
    NeedsRetry
}
