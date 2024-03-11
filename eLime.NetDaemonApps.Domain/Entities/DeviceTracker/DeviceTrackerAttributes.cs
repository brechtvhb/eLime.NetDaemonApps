using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.DeviceTracker;

public record DeviceTrackerAttributes
{
    [JsonPropertyName("source_type")]
    public string? SourceType { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("gps_accuracy")]
    public double? GpsAccuracy { get; init; }

    [JsonPropertyName("altitude")]
    public double? Altitude { get; init; }
}