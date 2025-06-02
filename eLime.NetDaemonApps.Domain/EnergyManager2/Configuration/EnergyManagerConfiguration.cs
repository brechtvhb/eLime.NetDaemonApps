using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;

public class EnergyManagerConfiguration
{
    public EnergyManagerConfiguration(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, EnergyManagerConfig config)
    {
        HaContext = haContext;
        Logger = logger;
        Scheduler = scheduler;
        FileStorage = fileStorage;
        MqttEntityManager = mqttEntityManager;

        Timezone = config.Timezone;
        DebounceDuration = TimeSpan.FromSeconds(1);

        Grid = new GridConfiguration(haContext, config.Grid);
        SolarProductionRemainingTodaySensor = NumericSensor.Create(haContext, config.SolarProductionRemainingTodayEntity);
        Consumers = config.Consumers.Select(c => new EnergyConsumerConfiguration(haContext, c)).ToList();
        BatteryManager = new BatteryManagerConfiguration(haContext, config.BatteryManager);

    }
    public IHaContext HaContext { get; set; }
    public ILogger Logger { get; set; }
    public IScheduler Scheduler { get; set; }
    public IFileStorage FileStorage { get; set; }
    public IMqttEntityManager MqttEntityManager { get; set; }
    public string Timezone { get; set; }
    public TimeSpan DebounceDuration { get; set; }

    public GridConfiguration Grid { get; set; }
    public NumericSensor SolarProductionRemainingTodaySensor { get; set; }

    public List<EnergyConsumerConfiguration> Consumers { get; set; }
    public BatteryManagerConfiguration BatteryManager { get; set; }

}