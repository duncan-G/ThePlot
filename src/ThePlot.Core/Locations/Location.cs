namespace ThePlot.Core.Locations;

public sealed class Location
{
    private Location()
    {
    }

    public Guid Id { get; private init; }
    public string Description { get; private init; } = null!;

    public static Location Create(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Location
        {
            Id = Guid.CreateVersion7(),
            Description = description.Trim()
        };
    }
}
