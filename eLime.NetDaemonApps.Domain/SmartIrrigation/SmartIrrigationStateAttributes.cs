using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public record SmartIrrigationStateAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("need_water_zones")]
    public List<string>? NeedWaterZones { get; init; }
    [JsonPropertyName("critical_need_water_zones")]
    public List<string>? CriticalNeedWaterZones { get; init; }
    [JsonPropertyName("watering_ongoing_zones")]
    public List<string>? WateringOngoingZones { get; init; }
}