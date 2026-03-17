namespace ThePlot.Core.Voices;

public sealed class Voice
{
    private Voice()
    {
    }

    public Guid Id { get; private init; }
    public string Description { get; private init; } = null!;

    public static Voice Create(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Voice
        {
            Id = Guid.CreateVersion7(),
            Description = description.Trim()
        };
    }
}
