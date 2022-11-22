using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Scenes;

public record SceneCreateParameters
{
    ///<summary>The entity_id of the new scene. eg: all_lights</summary>
    [JsonPropertyName("scene_id")]
    public string? SceneId { get; init; }

    ///<summary>The entities to control with the scene. eg: {"light.tv_back_light": "on", "light.ceiling": {"state": "on", "brightness": 200}}</summary>
    [JsonPropertyName("entities")]
    public object? Entities { get; init; }

    ///<summary>The entities of which a snapshot is to be taken eg: ["light.ceiling", "light.kitchen"]</summary>
    [JsonPropertyName("snapshot_entities")]
    public object? SnapshotEntities { get; init; }
}