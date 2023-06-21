using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class EntityOptions
{
    [JsonPropertyName("device")]
    public Device? Device { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }
}

public class EntityOptionsWithoutDevice
{
    [JsonPropertyName("icon")]
    public string Icon { get; set; }
}