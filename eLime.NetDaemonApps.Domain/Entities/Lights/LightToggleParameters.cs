using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Lights;

public record LightToggleParameters
{
    ///<summary>Duration it takes to get to next state.</summary>
    [JsonPropertyName("transition")]
    public long? Transition { get; init; }

    ///<summary>Color for the light in RGB-format. eg: [255, 100, 100]</summary>
    [JsonPropertyName("rgb_color")]
    public object? RgbColor { get; init; }

    ///<summary>A human readable color name.</summary>
    [JsonPropertyName("color_name")]
    public string? ColorName { get; init; }

    ///<summary>Color for the light in hue/sat format. Hue is 0-360 and Sat is 0-100. eg: [300, 70]</summary>
    [JsonPropertyName("hs_color")]
    public object? HsColor { get; init; }

    ///<summary>Color for the light in XY-format. eg: [0.52, 0.43]</summary>
    [JsonPropertyName("xy_color")]
    public object? XyColor { get; init; }

    ///<summary>Color temperature for the light in mireds.</summary>
    [JsonPropertyName("color_temp")]
    public long? ColorTemp { get; init; }

    ///<summary>Color temperature for the light in Kelvin.</summary>
    [JsonPropertyName("kelvin")]
    public long? Kelvin { get; init; }

    ///<summary>Number indicating level of white.</summary>
    [JsonPropertyName("white_value")]
    public long? WhiteValue { get; init; }

    ///<summary>Number indicating brightness, where 0 turns the light off, 1 is the minimum brightness and 255 is the maximum brightness supported by the light.</summary>
    [JsonPropertyName("brightness")]
    public long? Brightness { get; init; }

    ///<summary>Number indicating percentage of full brightness, where 0 turns the light off, 1 is the minimum brightness and 100 is the maximum brightness supported by the light.</summary>
    [JsonPropertyName("brightness_pct")]
    public long? BrightnessPct { get; init; }

    ///<summary>Name of a light profile to use. eg: relax</summary>
    [JsonPropertyName("profile")]
    public string? Profile { get; init; }

    ///<summary>If the light should flash.</summary>
    [JsonPropertyName("flash")]
    public string? Flash { get; init; }

    ///<summary>Light effect.</summary>
    [JsonPropertyName("effect")]
    public string? Effect { get; init; }
}