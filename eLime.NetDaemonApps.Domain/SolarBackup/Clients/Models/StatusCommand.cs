using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class StatusCommand
{
    public required Command Command { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Command
{
    [JsonStringEnumMemberName("shutdown")]
    Shutdown,
    [JsonStringEnumMemberName("reboot")]
    Reboot,
}