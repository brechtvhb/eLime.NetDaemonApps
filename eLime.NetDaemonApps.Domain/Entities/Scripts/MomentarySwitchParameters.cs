using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Scripts;

public class MomentarySwitchParameters
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; }
}