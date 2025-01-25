namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class Task
{
    public required string Upid { get; set; }
    public string? Status { get; set; }
}

public class TaskList
{
    public required List<Task> Data { get; set; } = [];
}