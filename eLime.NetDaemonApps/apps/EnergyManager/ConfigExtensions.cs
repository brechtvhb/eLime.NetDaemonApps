using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.EnergyManager;

public static class ConfigExtensions
{

    public static Domain.EnergyManager.EnergyManager ToEntities(this EnergyManagerConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger logger)
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
                energyConsumer = new SimpleEnergyConsumer(logger, consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, consumer.Simple.PeakLoad);
            }

            if (consumer.Cooling != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Cooling.SocketEntity);
                var temperatureSensor = new NumericEntity(ha, consumer.Cooling.TemperatureSensor);

                energyConsumer = new CoolingEnergyConsumer(logger, consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, consumer.Cooling.PeakLoad, temperatureSensor, consumer.Cooling.TargetTemperature, consumer.Cooling.MaxTemperature);
            }
            if (consumer.Triggered != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Triggered.SocketEntity);
                var stateSensor = TextSensor.Create(ha, consumer.Triggered.StateSensor);
                var stateMap = consumer.Triggered.PeakLoads.Select(x => (x.State, x.PeakLoad)).ToList();

                energyConsumer = new TriggeredEnergyConsumer(logger, consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, socket, stateMap, stateSensor, consumer.Triggered.StartState, consumer.Triggered.CompletedState, consumer.Triggered.CriticalState, consumer.Triggered.CanForceShutdown, consumer.Triggered.ShutDownOnComplete);
            }

            if (consumer.CarCharger != null)
            {
                var cars = new List<Car>();
                foreach (var car in consumer.CarCharger.Cars)
                {
                    var batteryPercentageSensor = new NumericEntity(ha, car.BatteryPercentageSensor);
                    var maxBatteryPercentageSensor = !String.IsNullOrWhiteSpace(car.MaxBatteryPercentageSensor) ? new NumericEntity(ha, car.MaxBatteryPercentageSensor) : null;
                    var cableConnectedSensor = new BinarySensor(ha, car.CableConnectedSensor);
                    cars.Add(new Car(car.Name, car.Supports3Phase, car.BatteryCapacity, car.IgnoreStateOnForceCharge, batteryPercentageSensor, maxBatteryPercentageSensor, cableConnectedSensor));
                }

                var currentEntity = InputNumberEntity.Create(ha, consumer.CarCharger.CurrentEntity);
                var stateSensor = TextSensor.Create(ha, consumer.CarCharger.StateSensor);

                energyConsumer = new CarChargerEnergyConsumer(logger, consumer.Name, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, consumer.CarCharger.MinimumCurrent, consumer.CarCharger.MaximumCurrent, consumer.CarCharger.OffCurrent, currentEntity, stateSensor, cars, scheduler);
            }


            if (energyConsumer != null)
                consumers.Add(energyConsumer);
        }

        var gridMonitor = new GridMonitor(scheduler, new NumericEntity(ha, config.Grid.VoltageEntity), NumericSensor.Create(ha, config.Grid.ImportEntity), NumericSensor.Create(ha, config.Grid.ExportEntity), new NumericEntity(ha, config.Grid.PeakImportEntity));
        var entity = new Domain.EnergyManager.EnergyManager(ha, logger, scheduler, mqttEntityManager, fileStorage, gridMonitor, new NumericEntity(ha, config.SolarProductionRemainingTodayEntity), consumers, phoneToNotify, TimeSpan.FromSeconds(5));
        return entity;
    }

    public static TimeWindow ToEntities(this TimeWindowConfig config, IHaContext ha)
    {
        var activeEntity = new BinarySensor(ha, config.ActiveEntity);
        return new TimeWindow(activeEntity, new TimeOnly(0, 0).Add(config.Start), new TimeOnly(0, 0).Add(config.End));
    }

}