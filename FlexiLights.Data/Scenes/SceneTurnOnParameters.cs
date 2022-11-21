using System.Text.Json.Serialization;

namespace FlexiLights.Data.Scenes;

public record SceneTurnOnParameters
{
    ///<summary>Transition duration it takes to bring devices to the state defined in the scene.</summary>
    [JsonPropertyName("transition")]
    public double? Transition { get; init; }
}