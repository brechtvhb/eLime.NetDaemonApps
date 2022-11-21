using System.Text.Json.Serialization;

namespace FlexiLights.Data.Lights;

public record LightAttributes
{
    [JsonPropertyName("attribution")]
    public string? Attribution { get; init; }

    [JsonPropertyName("brightness")]
    public double? Brightness { get; init; }

    [JsonPropertyName("color_mode")]
    public string? ColorMode { get; init; }

    [JsonPropertyName("color_temp")]
    public double? ColorTemp { get; init; }

    [JsonPropertyName("entity_id")]
    public object? EntityId { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("hs_color")]
    public object? HsColor { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("max_mireds")]
    public double? MaxMireds { get; init; }

    [JsonPropertyName("min_mireds")]
    public double? MinMireds { get; init; }

    [JsonPropertyName("off_brightness")]
    public object? OffBrightness { get; init; }

    [JsonPropertyName("rgb_color")]
    public object? RgbColor { get; init; }

    [JsonPropertyName("supported_color_modes")]
    public object? SupportedColorModes { get; init; }

    [JsonPropertyName("supported_features")]
    public double? SupportedFeatures { get; init; }

    [JsonPropertyName("xy_color")]
    public object? XyColor { get; init; }
}