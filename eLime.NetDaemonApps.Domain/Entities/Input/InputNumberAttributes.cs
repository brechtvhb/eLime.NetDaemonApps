using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Input;

public partial record InputNumberAttributes
{
    [JsonPropertyName("initial")]
    public double? Initial { get; init; }

    [JsonPropertyName("editable")]
    public bool? Editable { get; init; }

    [JsonPropertyName("min")]
    public double? Min { get; init; }

    [JsonPropertyName("max")]
    public double? Max { get; init; }

    [JsonPropertyName("step")]
    public double? Step { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }
}