using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Covers;

public record CoverAttributes
{
    [JsonPropertyName("current_position")]
    public double? CurrentPosition { get; init; }

    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("supported_features")]
    public double? SupportedFeatures { get; init; }
}