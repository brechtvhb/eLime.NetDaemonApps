using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Scripts;

public class WakeUpPcParameters
{
    [JsonPropertyName("mac_address")]
    public string MacAddress { get; set; }
    [JsonPropertyName("broadcast_address")]
    public string BroadcastAddress { get; set; }
}