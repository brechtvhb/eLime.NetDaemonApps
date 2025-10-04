using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Select;

public record SelectEntitySelectOptionParameters
{
    [JsonPropertyName("option")]
    public string Option { get; init; }
}