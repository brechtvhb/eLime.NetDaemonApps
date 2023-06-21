using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.Services;

public record NotifyPhoneParameters
{
    ///<summary>Message body of the notification. eg: The garage door has been open for 10 minutes.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    ///<summary>Title for your notification. eg: Your Garage Door Friend</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    ///<summary>An array of targets to send the notification to. Optional depending on the platform. eg: platform specific</summary>
    [JsonPropertyName("target")]
    public object? Target { get; init; }

    ///<summary>Extended information for notification. Optional depending on the platform. eg: platform specific</summary>
    [JsonPropertyName("data")]
    public NotifyPhoneData? Data { get; init; }
}

public record NotifyPhoneData
{
    [JsonPropertyName("channel")]
    public string? Channel { get; init; }
    [JsonPropertyName("importance")]
    public string? Importance { get; init; }
    [JsonPropertyName("visibility")]
    public string? Visibility { get; init; }
    [JsonPropertyName("ttl")]
    public int Ttl { get; init; }
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }
}