using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataStoreStatus
{
    [JsonStringEnumMemberName("online")]
    Online,
    [JsonStringEnumMemberName("available")]
    Available,
    [JsonStringEnumMemberName("unknown")]
    Offline,
}