using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class Device
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; }
    [JsonPropertyName("identifiers")]
    public List<string> Identifiers { get; set; }
}