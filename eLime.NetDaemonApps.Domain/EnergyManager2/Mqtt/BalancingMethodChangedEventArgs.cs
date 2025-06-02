using eLime.NetDaemonApps.Domain.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;

public class BalancingMethodChangedEventArgs : EventArgs
{
    public required BalancingMethod BalancingMethod;

    public static BalancingMethodChangedEventArgs Create(BalancingMethod balancingMethod) => new() { BalancingMethod = balancingMethod };
}