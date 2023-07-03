using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public record EnergyConsumerAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }
    [JsonPropertyName("last_run")]
    public string? LastRun { get; init; }
}