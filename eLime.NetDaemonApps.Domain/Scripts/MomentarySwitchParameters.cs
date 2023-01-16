using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Scripts;

public class MomentarySwitchParameters
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; }
}

public class WakeUpPcParameters
{
    [JsonPropertyName("mac_address")]
    public string MacAddress { get; set; }
}