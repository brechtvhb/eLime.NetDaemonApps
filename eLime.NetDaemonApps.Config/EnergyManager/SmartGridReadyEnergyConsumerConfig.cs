namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class SmartGridReadyEnergyConsumerConfig
{
    public string SmartGridModeEntity { get; set; }
    public double PeakLoad { get; set; }

    public List<TimeWindowConfig> BlockedTimeWindows { get; set; } = [];

    public string StateSensor { get; set; }
    public string EnergyNeededState { get; set; }
    public string CriticalEnergyNeededState { get; set; }
}