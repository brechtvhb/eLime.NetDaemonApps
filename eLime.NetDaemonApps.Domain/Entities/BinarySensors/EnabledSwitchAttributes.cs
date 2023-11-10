using eLime.NetDaemonApps.Domain.Mqtt;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record EnabledSwitchAttributes
{
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }

    [JsonPropertyName("device")]
    public Device Device { get; set; }
}