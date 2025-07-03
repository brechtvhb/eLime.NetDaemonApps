namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;

public class DynamicEnergyConsumerBalancingMethodBasedLoads
{
    public List<BalancingMethod> BalancingMethods { get; set; } = [];
    public double SwitchOnLoad { get; set; }
    public double SwitchOffLoad { get; set; }

    public LoadTimeFrames? LoadTimeFrameToCheckOnRebalance { get; set; }

}