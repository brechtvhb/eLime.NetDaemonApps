using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.EnergyManager;

public static class ConfigExtensions
{

    public static Domain.EnergyManager.EnergyManager ToEntities(this EnergyManagerConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, ILogger logger)
    {
        var phoneToNotify = config.PhoneToNotify;

        var consumers = new List<EnergyConsumer>();
        foreach (var consumer in config.Consumers)
        {
            var powerUsageEntity = new NumericEntity(ha, consumer.PowerUsageEntity);
            var criticallyNeededEntity = !String.IsNullOrWhiteSpace(consumer.CriticallyNeededEntity) ? new BinarySensor(ha, consumer.CriticallyNeededEntity) : null;
            var timeWindows = consumer.TimeWindows?.Select(x => x.ToEntities(ha))?.ToList() ?? new List<TimeWindow>();

            EnergyConsumer energyConsumer = null;
            if (consumer.Simple != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Simple.SocketEntity);
                energyConsumer = new SimpleEnergyConsumer(consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.PreferSolar, consumer.SwitchOnLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, consumer.Simple.PeakLoad);
            }

            if (consumer.Cooling != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Cooling.SocketEntity);
                var temperatureSensor = new NumericEntity(ha, consumer.Cooling.TemperatureSensor);

                energyConsumer = new CoolingEnergyConsumer(consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.PreferSolar, consumer.SwitchOnLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, consumer.Cooling.PeakLoad, temperatureSensor, consumer.Cooling.TargetTemperature, consumer.Cooling.MaxTemperature);
            }
            if (consumer.Triggered != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Triggered.SocketEntity);
                var stateSensor = TextSensor.Create(ha, consumer.Triggered.StateSensor);
                var stateMap = consumer.Triggered.PeakLoads.Select(x => (x.State, x.PeakLoad)).ToList();

                energyConsumer = new TriggeredEnergyConsumer(consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.PreferSolar, consumer.SwitchOnLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, stateMap, stateSensor, consumer.Triggered.StartState, consumer.Triggered.CriticalState, consumer.Triggered.CanForceShutdown);
            }


            if (energyConsumer != null)
                consumers.Add(energyConsumer);
        }

        var gridMonitor = new GridMonitor(scheduler, new NumericEntity(ha, config.Grid.VoltageEntity), NumericSensor.Create(ha, config.Grid.ImportEntity), NumericSensor.Create(ha, config.Grid.ExportEntity), new NumericEntity(ha, config.Grid.PeakImportEntity));
        var entity = new Domain.EnergyManager.EnergyManager(ha, logger, scheduler, mqttEntityManager, gridMonitor, new NumericEntity(ha, config.SolarProductionRemainingTodayEntity), consumers, phoneToNotify, TimeSpan.FromSeconds(5));
        return entity;
    }

    public static TimeWindow ToEntities(this TimeWindowConfig config, IHaContext ha)
    {
        var isActiveEntity = new BinarySensor(ha, config.IsActiveEntity);
        return new TimeWindow(isActiveEntity, config.Start, config.End);
    }

}