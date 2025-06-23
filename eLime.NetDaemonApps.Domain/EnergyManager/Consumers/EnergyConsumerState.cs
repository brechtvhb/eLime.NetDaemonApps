namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public enum EnergyConsumerState
{
    Unknown,
    Off,
    Running,
    NeedsEnergy,
    CriticallyNeedsEnergy
}