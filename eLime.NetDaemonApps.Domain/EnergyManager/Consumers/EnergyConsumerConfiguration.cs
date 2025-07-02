using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Cooling;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Simple;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel;
using BalancingMethod = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.BalancingMethod;
using DynamicEnergyConsumerBalancingMethodBasedLoads = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger.DynamicEnergyConsumerBalancingMethodBasedLoads;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public class EnergyConsumerConfiguration
{
    public EnergyConsumerConfiguration(IHaContext haContext, EnergyConsumerConfig config)
    {
        Name = config.Name;
        ConsumerGroups = config.ConsumerGroups;
        PowerConsumptionSensor = NumericSensor.Create(haContext, config.PowerUsageEntity);
        SwitchOnLoad = config.SwitchOnLoad;
        SwitchOffLoad = config.SwitchOffLoad;
        LoadTimeFramesToCheckOnStart = Enum<LoadTimeFrames>.CastEnumList(config.LoadTimeFramesToCheckOnStart);
        LoadTimeFramesToCheckOnStop = Enum<LoadTimeFrames>.CastEnumList(config.LoadTimeFramesToCheckOnStop);
        DynamicBalancingMethodBasedLoads = config.DynamicBalancingMethodBasedLoads.Select(x => new DynamicEnergyConsumerBalancingMethodBasedLoads { BalancingMethods = Enum<BalancingMethod>.CastEnumList(x.BalancingMethods), SwitchOnLoad = x.SwitchOnLoad, SwitchOffLoad = x.SwitchOffLoad }).ToList();
        MinimumRuntime = config.MinimumRuntime;
        MaximumRuntime = config.MaximumRuntime;
        MinimumTimeout = config.MinimumTimeout;
        MaximumTimeout = config.MaximumTimeout;
        CriticallyNeededSensor = !string.IsNullOrWhiteSpace(config.CriticallyNeededEntity) ? BinarySensor.Create(haContext, config.CriticallyNeededEntity) : null;
        TimeWindows = config.TimeWindows.Select(x => new TimeWindowConfiguration(haContext, x)).ToList();
        Simple = config.Simple != null ? new SimpleEnergyConsumerConfiguration(haContext, config.Simple) : null;
        Cooling = config.Cooling != null ? new CoolingEnergyConsumerConfiguration(haContext, config.Cooling) : null;
        Triggered = config.Triggered != null ? new TriggeredEnergyConsumerConfiguration(haContext, config.Triggered) : null;
        CarCharger = config.CarCharger != null ? new CarChargerEnergyConsumerConfiguration(haContext, config.CarCharger) : null;
    }
    public string Name { get; set; }
    public List<string> ConsumerGroups { get; set; }
    public NumericSensor PowerConsumptionSensor { get; set; }
    public double SwitchOnLoad { get; set; }
    public double SwitchOffLoad { get; set; }
    public List<LoadTimeFrames> LoadTimeFramesToCheckOnStart { get; set; }
    public List<LoadTimeFrames> LoadTimeFramesToCheckOnStop { get; set; }
    public List<DynamicEnergyConsumerBalancingMethodBasedLoads> DynamicBalancingMethodBasedLoads { get; set; }
    public TimeSpan? MinimumRuntime { get; set; }
    public TimeSpan? MaximumRuntime { get; set; }
    public TimeSpan? MinimumTimeout { get; set; }
    public TimeSpan? MaximumTimeout { get; set; }
    public BinarySensor? CriticallyNeededSensor { get; set; }
    public List<TimeWindowConfiguration> TimeWindows { get; set; }

    public SimpleEnergyConsumerConfiguration? Simple { get; set; }
    public CoolingEnergyConsumerConfiguration? Cooling { get; set; }
    public TriggeredEnergyConsumerConfiguration? Triggered { get; set; }
    public CarChargerEnergyConsumerConfiguration? CarCharger { get; set; }
}