namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

public class BalancingMethodChangedEventArgs : EventArgs
{
    public required BalancingMethod BalancingMethod;

    public static BalancingMethodChangedEventArgs Create(BalancingMethod balancingMethod) => new() { BalancingMethod = balancingMethod };
}