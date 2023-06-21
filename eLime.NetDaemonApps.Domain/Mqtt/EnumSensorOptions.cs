using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class EnumSensorOptions : EntityOptions
{
    [JsonPropertyName("options")]
    public List<string> Options { get; set; }
}