using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Weather;

public record WeatherAttributes
{
    [JsonPropertyName("attribution")]
    public string? Attribution { get; init; }

    //Hmmhhhh?
    [JsonPropertyName("forecast")]
    public object? Forecast { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("humidity")]
    public double? Humidity { get; init; }

    [JsonPropertyName("ozone")]
    public double? Ozone { get; init; }

    [JsonPropertyName("pressure")]
    public double? Pressure { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("visibility")]
    public double? Visibility { get; init; }

    [JsonPropertyName("wind_bearing")]
    public double? WindBearing { get; init; }

    [JsonPropertyName("wind_speed")]
    public double? WindSpeed { get; init; }
}