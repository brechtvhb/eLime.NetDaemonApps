using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record FlexiScreenEnabledSwitchAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("last_automated_state_change")]
    public string? LastAutomatedStateChange { get; init; }
    [JsonPropertyName("last_manual_state_change")]
    public string? LastManualStateChange { get; init; }

    [JsonPropertyName("last_state_change_triggered_by")]
    public string? LastStateChangeTriggeredBy { get; init; }
}