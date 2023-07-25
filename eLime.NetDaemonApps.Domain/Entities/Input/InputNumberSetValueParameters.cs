using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Input;

public record InputNumberSetValueParameters
{
    [JsonPropertyName("value")]
    public double Value { get; init; }
}