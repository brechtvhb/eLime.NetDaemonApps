using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class EntityOptions
{
    [JsonPropertyName("device")]
    public Device Device { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }
}

public class ButtonOptions : EntityOptions
{
    [JsonPropertyName("payload_press")]
    public string PayloadPress { get; set; }
}