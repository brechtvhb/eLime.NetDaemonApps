using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Buttons;

public record ButtonAttributes
{
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    [JsonPropertyName("restored")]
    public bool? Restored { get; init; }

    [JsonPropertyName("supported_features")]
    public double? SupportedFeatures { get; init; }
}