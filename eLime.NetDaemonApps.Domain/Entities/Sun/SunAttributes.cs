using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Sun;

public record SunAttributes
{
    [JsonPropertyName("azimuth")]
    public double? Azimuth { get; init; }

    [JsonPropertyName("elevation")]
    public double? Elevation { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("next_dawn")]
    public string? NextDawn { get; init; }

    [JsonPropertyName("next_dusk")]
    public string? NextDusk { get; init; }

    [JsonPropertyName("next_midnight")]
    public string? NextMidnight { get; init; }

    [JsonPropertyName("next_noon")]
    public string? NextNoon { get; init; }

    [JsonPropertyName("next_rising")]
    public string? NextRising { get; init; }

    [JsonPropertyName("next_setting")]
    public string? NextSetting { get; init; }

    [JsonPropertyName("rising")]
    public bool? Rising { get; init; }
}