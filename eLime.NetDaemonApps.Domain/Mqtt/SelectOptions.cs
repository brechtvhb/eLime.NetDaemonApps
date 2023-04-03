using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class SelectOptions : EntityOptions
{
    [JsonPropertyName("options")]
    public List<string> Options { get; set; }
}