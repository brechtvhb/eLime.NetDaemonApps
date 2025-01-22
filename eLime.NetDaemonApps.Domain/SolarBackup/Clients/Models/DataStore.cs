namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class DataStore
{
    public required string Id { get; set; }
    public required string Storage { get; set; }
    public required DataStoreStatus Status { get; set; }
    public required string Content { get; set; }
}

public class DataStoreList
{
    public required List<DataStore> Data { get; set; } = [];
}