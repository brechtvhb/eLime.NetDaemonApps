namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class TaskResponse
{
    //God know why the ID is named Data ...
    public required string Data { get; set; }
    public required int Success { get; set; }
}