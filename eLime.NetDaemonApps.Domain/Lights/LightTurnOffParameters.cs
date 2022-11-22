using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Lights;

public record LightTurnOffParameters
{
    ///<summary>Duration it takes to get to next state.</summary>
    [JsonPropertyName("transition")]
    public double? Transition { get; init; }

    ///<summary>If the light should flash.</summary>
    [JsonPropertyName("flash")]
    public string? Flash { get; init; }
}