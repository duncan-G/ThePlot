using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using LocationType = ThePlot.Core.Locations.LocationType;

namespace ThePlot.Infrastructure;

public sealed class LocationTypeConverter : ValueConverter<LocationType, string>
{
    public static readonly LocationTypeConverter Instance = new();

    private LocationTypeConverter()
        : base(v => ToProvider(v), v => FromProvider(v))
    {
    }

    private static string ToProvider(LocationType value) =>
        value switch
        {
            LocationType.Int => "INT",
            LocationType.Ext => "EXT",
            LocationType.IE => "I_E",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

    private static LocationType FromProvider(string value) =>
        value switch
        {
            "INT" => LocationType.Int,
            "EXT" => LocationType.Ext,
            "I_E" => LocationType.IE,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
}
