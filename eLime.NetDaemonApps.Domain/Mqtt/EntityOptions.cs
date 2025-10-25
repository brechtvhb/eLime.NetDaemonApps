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

public class NumberOptions : EntityOptions
{
    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }

    [JsonPropertyName("step")]
    public double Step { get; set; }

    [JsonPropertyName("unit_of_measurement")]
    public string UnitOfMeasurement { get; set; }
}