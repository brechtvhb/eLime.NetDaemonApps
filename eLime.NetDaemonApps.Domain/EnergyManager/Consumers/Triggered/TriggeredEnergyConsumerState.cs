using eLime.NetDaemonApps.Config.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;

public class TriggeredEnergyConsumerState
{
    public TriggeredEnergyConsumerState(TriggeredEnergyConsumerStateConfig config)
    {
        Name = config.Name;
        PeakLoad = config.PeakLoad;
        IsRunning = config.IsRunning;
    }
    public string Name { get; set; }
    public double PeakLoad { get; set; }
    public bool IsRunning { get; set; }
}