using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class Task
{
    public required string Upid { get; set; }
    public required TaskStatus Status { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskStatus
{
    Ongoing,
    [JsonStringEnumMemberName("ok")]
    Ok,
}

public class TaskList
{
    public required List<Task> Data { get; set; } = [];
}