namespace ThePlot.Infrastructure.Vllm;

public sealed class VllmOptions
{
    public const string SectionName = "Vllm";

    public string SupervisordUrl { get; set; } = "";
    public TimeSpan ModelStartupTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HealthCheckPollInterval { get; set; } = TimeSpan.FromSeconds(3);
}
