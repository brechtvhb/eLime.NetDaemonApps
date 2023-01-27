using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record SmartWasherSwitchAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("program")]
    public string? Program { get; init; }


    [JsonPropertyName("state")]
    public string? State { get; init; }


    [JsonPropertyName("last_state_change")]
    public string? LasStateChange { get; init; }

    [JsonPropertyName("eta")]
    public string? Eta { get; init; }

}