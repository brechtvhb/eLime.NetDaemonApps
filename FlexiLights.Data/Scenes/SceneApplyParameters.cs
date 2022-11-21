using System.Text.Json.Serialization;

namespace FlexiLights.Data.Scenes;

public record SceneApplyParameters
{
    ///<summary>The entities and the state that they need to be. eg: {"light.kitchen": "on", "light.ceiling": {"state": "on", "brightness": 80}}</summary>
    [JsonPropertyName("entities")]
    public object? Entities { get; init; }

    ///<summary>Transition duration it takes to bring devices to the state defined in the scene.</summary>
    [JsonPropertyName("transition")]
    public long? Transition { get; init; }
}