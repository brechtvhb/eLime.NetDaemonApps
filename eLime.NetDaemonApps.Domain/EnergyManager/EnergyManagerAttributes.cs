using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public record EnergyManagerAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("need_energy_consumers")]
    public List<String>? NeedEnergyConsumers { get; init; }
    [JsonPropertyName("critical_need_energy_consumers")]
    public List<String>? CriticalNeedEnergyConsumers { get; init; }
    [JsonPropertyName("running_consumers")]
    public List<String>? RunningConsumers { get; init; }
}