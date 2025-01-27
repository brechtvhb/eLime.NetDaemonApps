namespace eLime.NetDaemonApps.Config;

public class SolarBackupConfig
{
    public SolarBackupSynologyConfig Synology { get; set; }
    public SolarBackupPveConfig Pve { get; set; }
    public SolarBackupPbsConfig Pbs { get; set; }
    public TimeSpan CriticalBackupInterval { get; set; }
}

public class SolarBackupSynologyConfig
{
    public string Mac { get; set; }
    public string BroadcastAddress { get; set; }
    public string ShutDownButton { get; set; }
}
public class SolarBackupPveConfig
{
    public string Url { get; set; }
    public string Token { get; set; }
    public string Cluster { get; set; }
    public string StorageId { get; set; }
    public string StorageName { get; set; }
}
public class SolarBackupPbsConfig
{
    public string Url { get; set; }
    public string Token { get; set; }
    public string DataStore { get; set; }
    public string VerifyJobId { get; set; }
    public string PruneJobId { get; set; }
}