using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public record SmartIrrigationStateAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("need_water_zones")]
    public List<String>? NeedWaterZones { get; init; }
    [JsonPropertyName("critical_need_water_zones")]
    public List<String>? CriticalNeedWaterZones { get; init; }
    [JsonPropertyName("watering_ongoing_zones")]
    public List<String>? WateringOngoingZones { get; init; }
}