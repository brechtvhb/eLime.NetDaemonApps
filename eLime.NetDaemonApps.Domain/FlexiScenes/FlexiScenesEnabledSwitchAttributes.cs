using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.FlexiScenes;

public record FlexiScenesEnabledSwitchAttributes : EnabledSwitchAttributes
{
    [JsonPropertyName("ignore_presence_until")]
    public string? IgnorePresenceUntil { get; init; }

    [JsonPropertyName("turn_off_at")]
    public string? TurnOffAt { get; init; }

    [JsonPropertyName("initiated_by")]
    public string? InitiatedBy { get; init; }

    [JsonPropertyName("initial_flexi_Scene")]
    public string? InitialFlexiScene { get; init; }

    [JsonPropertyName("current_flexi_scene")]
    public string? CurrentFlexiScene { get; init; }

}