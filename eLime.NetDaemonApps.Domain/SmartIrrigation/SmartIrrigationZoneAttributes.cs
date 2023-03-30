using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public record SmartIrrigationZoneAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("watering_started_at")]
    public string? WateringStartedAt { get; init; }
    [JsonPropertyName("last_watering")]
    public string? LastWatering { get; init; }
}