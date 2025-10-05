namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class EnergyConsumerConfig
{
    public string Name { get; set; }
    public List<string> ConsumerGroups { get; set; } = [];

    //TODO: Rename dunglish to PowerConsumptionSensor
    public string PowerUsageEntity { get; set; }
    public double SwitchOnLoad { get; set; }
    public double SwitchOffLoad { get; set; }
    public List<LoadTimeFrames> LoadTimeFramesToCheckOnStart { get; set; } = [];
    public List<LoadTimeFrames> LoadTimeFramesToCheckOnStop { get; set; } = [];
    public LoadTimeFrames? LoadTimeFrameToCheckOnRebalance { get; set; }
    public List<DynamicEnergyConsumerBalancingMethodBasedLoadsConfig> DynamicBalancingMethodBasedLoads { get; set; } = [];

    public TimeSpan? MinimumRuntime { get; set; }
    public TimeSpan? MaximumRuntime { get; set; }
    public TimeSpan? MinimumTimeout { get; set; }
    public TimeSpan? MaximumTimeout { get; set; }

    //Extra entity to force something to run (Eg : pond pump when it is freezing), rename to CriticallyNeededSensor
    public string? CriticallyNeededEntity { get; set; }

    public List<TimeWindowConfig> TimeWindows { get; set; } = [];

    public SimpleEnergyConsumerConfig? Simple { get; set; }
    public CoolingEnergyConsumerConfig? Cooling { get; set; }
    public TriggeredEnergyConsumerConfig? Triggered { get; set; }
    public SmartGridReadyEnergyConsumerConfig? SmartGridReady { get; set; }
    public CarChargerEnergyConsumerConfig? CarCharger { get; set; }
}

public class DynamicEnergyConsumerBalancingMethodBasedLoadsConfig
{
    public List<BalancingMethod> BalancingMethods { get; set; } = [];
    public double SwitchOnLoad { get; set; }
    public double SwitchOffLoad { get; set; }
    public LoadTimeFrames? LoadTimeFrameToCheckOnRebalance { get; set; }
}

public enum BalancingMethod
{
    SolarSurplus,
    SolarOnly,
    MidPoint,
    SolarPreferred,
    MidPeak,
    NearPeak,
    MaximizeQuarterPeak
}

public enum LoadTimeFrames
{
    Now,
    Last30Seconds,
    LastMinute,
    Last2Minutes,
    Last5Minutes,
    SolarForecastNowCorrected, //SolarForecastNow
    SolarForecastNow50PercentCorrected,
    SolarForecast30MinutesCorrected, //SolarForecast30Minutes
    SolarForecast1HourCorrected, //SolarForecast1Hour
}