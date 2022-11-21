using System.Text.Json.Serialization;

namespace FlexiLights.Data.Numeric;

public record NumericSensorAttributes
{
    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("max")]
    public double? Max { get; init; }

    [JsonPropertyName("min")]
    public double? Min { get; init; }
    [JsonPropertyName("step")]
    public double? Step { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    [JsonPropertyName("unit_of_measurement")]
    public string? UnitOfMeasurement { get; init; }
}