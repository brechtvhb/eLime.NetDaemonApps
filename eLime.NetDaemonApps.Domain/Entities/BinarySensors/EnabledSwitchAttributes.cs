using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record EnabledSwitchAttributes
{

    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

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

    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }
}