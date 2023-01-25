using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record EnabledSwitchAttributes
{
    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }
}