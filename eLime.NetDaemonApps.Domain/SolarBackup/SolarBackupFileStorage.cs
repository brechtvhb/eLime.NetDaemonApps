namespace eLime.NetDaemonApps.Domain.SolarBackup;

internal class SolarBackupFileStorage
{
    public SolarBackupStatus State { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastBackupCompletedAt { get; set; }
}