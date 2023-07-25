namespace eLime.NetDaemonApps.Config.EnergyManager;

public class EnergyConsumerConfig
{
    public string Name { get; set; }

    public string PowerUsageEntity { get; set; }
    public double SwitchOnLoad { get; set; }
    public double SwitchOffLoad { get; set; }

    public TimeSpan? MinimumRuntime { get; set; }
    public TimeSpan? MaximumRuntime { get; set; }
    public TimeSpan? MinimumTimeout { get; set; }
    public TimeSpan? MaximumTimeout { get; set; }

    //Extra entity to force something to run (Eg : pond pump when it is freezing)
    public string CriticallyNeededEntity { get; set; }

    public List<TimeWindowConfig> TimeWindows { get; set; }

    public SimpleEnergyConsumerConfig? Simple { get; set; }
    public CoolingEnergyConsumerConfig? Cooling { get; set; }
    public TriggeredEnergyConsumerConfig? Triggered { get; set; }
    public CarChargerEnergyConsumerConfig? CarCharger{ get; set; }


}