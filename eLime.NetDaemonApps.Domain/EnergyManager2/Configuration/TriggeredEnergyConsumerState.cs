using eLime.NetDaemonApps.Config.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;

public class TriggeredEnergyConsumerState
{
    public TriggeredEnergyConsumerState(State config)
    {
        Name = config.Name;
        PeakLoad = config.PeakLoad;
        IsRunning = config.IsRunning;
    }
    public string Name { get; set; }
    public double PeakLoad { get; set; }
    public bool IsRunning { get; set; }
}