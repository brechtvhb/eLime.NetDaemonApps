using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;
using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManagerConfiguration
{
    public GridConfiguration Grid { get; private init; }
    public NumericSensor SolarProductionRemainingTodaySensor { get; private init; }

    public List<EnergyConsumerConfiguration> Consumers { get; private init; }
    public BatteryManagerConfiguration BatteryManager { get; private init; }
    public EnergyManagerContext Context { get; private init; }

    public EnergyManagerConfiguration(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, EnergyManagerConfig config, TimeSpan debounceDuration)
    {
        Context = new EnergyManagerContext(haContext, logger, scheduler, fileStorage, mqttEntityManager, config.Timezone, debounceDuration);
        Grid = new GridConfiguration(haContext, config.Grid);
        SolarProductionRemainingTodaySensor = NumericSensor.Create(haContext, config.SolarProductionRemainingTodayEntity);
        Consumers = config.Consumers.Select(c => new EnergyConsumerConfiguration(haContext, c)).ToList();
        BatteryManager = new BatteryManagerConfiguration(haContext, config.BatteryManager);

    }
}

public class EnergyManagerContext(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, string timezone, TimeSpan debounceDuration)
{
    public IHaContext HaContext { get; private init; } = haContext;
    public ILogger Logger { get; private init; } = logger;
    public IScheduler Scheduler { get; private init; } = scheduler;
    public IFileStorage FileStorage { get; private init; } = fileStorage;
    public IMqttEntityManager MqttEntityManager { get; private init; } = mqttEntityManager;
    public string Timezone { get; private init; } = timezone;
    public TimeSpan DebounceDuration { get; private init; } = debounceDuration;
}