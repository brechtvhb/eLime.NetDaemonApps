using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class NumericSensorOptions : EntityOptions
{
    [JsonPropertyName("unit_of_measurement")]
    public string UnitOfMeasurement { get; set; }

    [JsonPropertyName("state_class")]
    public string StateClass { get; set; }
}