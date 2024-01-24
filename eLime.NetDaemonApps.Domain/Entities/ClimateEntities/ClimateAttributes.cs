using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.ClimateEntities;

public record ClimateAttributes
{
    [JsonPropertyName("hvac_modes")]
    public IReadOnlyList<string>? HvacModes { get; init; }

    [JsonPropertyName("min_temp")]
    public double? MinTemp { get; init; }

    [JsonPropertyName("max_temp")]
    public double? MaxTemp { get; init; }

    [JsonPropertyName("target_temp_step")]
    public double? TargetTempStep { get; init; }

    [JsonPropertyName("current_temperature")]
    public double? CurrentTemperature { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("fan_modes")]
    public IReadOnlyList<string>? FanModes { get; init; }

    [JsonPropertyName("fan_mode")]
    public string? FanMode { get; init; }

}