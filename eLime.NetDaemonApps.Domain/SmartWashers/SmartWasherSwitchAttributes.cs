using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartWashers;

public record SmartWasherSwitchAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("program")]
    public string? Program { get; init; }


    [JsonPropertyName("washer_state")]
    public string? WasherState { get; init; }


    [JsonPropertyName("last_state_change")]
    public string? LasStateChange { get; init; }

    [JsonPropertyName("eta")]
    public string? Eta { get; init; }

}