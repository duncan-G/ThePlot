namespace ThePlot.Infrastructure.Vllm;

/// <summary>
/// Mutually exclusive sets of vLLM models that can occupy the GPU at once.
/// The worker swaps between sets via supervisord before each pipeline phase.
/// </summary>
public enum GpuModelSet
{
    None,
    VoiceDetermination,
    Tts
}
