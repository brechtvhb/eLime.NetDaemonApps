namespace eLime.NetDaemonApps.Domain.EnergyManager;

public enum CarChargerStates
{
    Available,
    Occupied,
    Charging
}
public enum CarChargingStates
{
    disconnected,
    no_power,
    stopped,
    starting,
    charging,
    complete
}