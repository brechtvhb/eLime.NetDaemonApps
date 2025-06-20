﻿using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using CarChargingMode = eLime.NetDaemonApps.Domain.EnergyManager.CarChargingMode;
using State = eLime.NetDaemonApps.Domain.EnergyManager.State;

namespace eLime.NetDaemonApps.apps.EnergyManager;

public static class ConfigExtensions
{
    public static Domain.EnergyManager.EnergyManager ToEntities(this EnergyManagerConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger logger)
    {
        var phoneToNotify = config.PhoneToNotify;

        var consumers = new List<EnergyConsumer>();
        var batteries = new List<Battery>();

        foreach (var consumer in config.Consumers)
        {
            var powerUsageEntity = new NumericEntity(ha, consumer.PowerUsageEntity);
            var criticallyNeededEntity = !String.IsNullOrWhiteSpace(consumer.CriticallyNeededEntity) ? new BinarySensor(ha, consumer.CriticallyNeededEntity) : null;
            var timeWindows = consumer.TimeWindows?.Select(x => x.ToEntities(ha))?.ToList() ?? [];

            EnergyConsumer energyConsumer = null;
            if (consumer.Simple != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Simple.SocketEntity);
                energyConsumer = new SimpleEnergyConsumer(logger, consumer.Name, consumer.ConsumerGroups, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, config.Timezone, socket, consumer.Simple.PeakLoad);
            }

            if (consumer.Cooling != null)
            {
                var socket = BinarySwitch.Create(ha, consumer.Cooling.SocketEntity);
                var temperatureSensor = new NumericEntity(ha, consumer.Cooling.TemperatureSensor);

                energyConsumer = new CoolingEnergyConsumer(logger, consumer.Name, consumer.ConsumerGroups, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, config.Timezone, socket, consumer.Cooling.PeakLoad, temperatureSensor, consumer.Cooling.TargetTemperature, consumer.Cooling.MaxTemperature);
            }
            if (consumer.Triggered != null)
            {
                var socket = !String.IsNullOrWhiteSpace(consumer.Triggered.SocketEntity) ? BinarySwitch.Create(ha, consumer.Triggered.SocketEntity) : null;
                var startButton = !String.IsNullOrWhiteSpace(consumer.Triggered.StartButton) ? new Button(ha, consumer.Triggered.StartButton) : null;
                BinarySwitch? pauseSwitch = null;

                if (!String.IsNullOrWhiteSpace(consumer.Triggered.PauseSwitch))
                    pauseSwitch = BinarySwitch.Create(ha, consumer.Triggered.PauseSwitch);

                var stateSensor = TextSensor.Create(ha, consumer.Triggered.StateSensor);
                var states = consumer.Triggered.States.Select(x => State.Create(x.Name, x.PeakLoad, x.IsRunning)).ToList();
                energyConsumer = new TriggeredEnergyConsumer(logger, consumer.Name, consumer.ConsumerGroups, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, config.Timezone, socket, startButton, pauseSwitch, states, stateSensor, consumer.Triggered.StartState, consumer.Triggered.PausedState, consumer.Triggered.CompletedState, consumer.Triggered.CriticalState, consumer.Triggered.CanPause, consumer.Triggered.ShutDownOnComplete);
            }

            if (consumer.CarCharger != null)
            {
                var cars = new List<Car>();
                foreach (var car in consumer.CarCharger.Cars)
                {
                    var carChargerSwitch = !String.IsNullOrWhiteSpace(car.ChargerSwitch) ? BinarySwitch.Create(ha, car.ChargerSwitch) : null;
                    var carCurrentEntity = !String.IsNullOrWhiteSpace(car.CurrentEntity) ? new InputNumberEntity(ha, car.CurrentEntity) : null;

                    var batteryPercentageSensor = new NumericEntity(ha, car.BatteryPercentageSensor);
                    var maxBatteryPercentageSensor = !String.IsNullOrWhiteSpace(car.MaxBatteryPercentageSensor) ? new NumericEntity(ha, car.MaxBatteryPercentageSensor) : null;
                    var cableConnectedSensor = BinarySensor.Create(ha, car.CableConnectedSensor);
                    var location = new DeviceTracker(ha, car.Location);
                    var chargingStateSensor = !String.IsNullOrWhiteSpace(car.ChargingStateSensor) ? TextSensor.Create(ha, car.ChargingStateSensor) : null;
                    var mode = Enum<CarChargingMode>.Cast(car.Mode);

                    cars.Add(new Car(car.Name, mode, carChargerSwitch, carCurrentEntity, car.MinimumCurrent, car.MaximumCurrent, chargingStateSensor, car.BatteryCapacity, batteryPercentageSensor, maxBatteryPercentageSensor, car.RemainOnAtFullBattery, cableConnectedSensor, car.AutoPowerOnWhenConnecting, location, scheduler));
                }

                var currentEntity = InputNumberEntity.Create(ha, consumer.CarCharger.CurrentEntity);
                var voltageEntity = !String.IsNullOrEmpty(consumer.CarCharger.VoltageEntity) ? new NumericEntity(ha, consumer.CarCharger.VoltageEntity) : null;
                var stateSensor = TextSensor.Create(ha, consumer.CarCharger.StateSensor);

                energyConsumer = new CarChargerEnergyConsumer(logger, consumer.Name, consumer.ConsumerGroups, powerUsageEntity, criticallyNeededEntity, consumer.SwitchOnLoad, consumer.SwitchOffLoad, consumer.MinimumRuntime, consumer.MaximumRuntime, consumer.MinimumTimeout, consumer.MaximumTimeout, timeWindows, config.Timezone,
                    consumer.CarCharger.MinimumCurrent, consumer.CarCharger.MaximumCurrent, consumer.CarCharger.OffCurrent, currentEntity, voltageEntity, stateSensor, cars, scheduler);
            }

            if (energyConsumer != null)
                consumers.Add(energyConsumer);
        }

        foreach (var batteryConfig in config.BatteryManager.Batteries)
        {
            var powerSensor = new NumericEntity(ha, batteryConfig.PowerSensor);
            var stateOfChargeSensor = new NumericEntity(ha, batteryConfig.StateOfChargeSensor);
            var totalEnergyChargedSensor = new NumericEntity(ha, batteryConfig.StateOfChargeSensor);
            var totalEnergyDischargedSensor = new NumericEntity(ha, batteryConfig.StateOfChargeSensor);
            var maxChargePowerEntity = new InputNumberEntity(ha, batteryConfig.MaxChargePowerEntity);
            var maxDischargePowerEntity = new InputNumberEntity(ha, batteryConfig.MaxDischargePowerEntity);

            var battery = new Battery(logger, scheduler, batteryConfig.Name, batteryConfig.Capacity, batteryConfig.MaxChargePower, batteryConfig.MaxDischargePower,
                powerSensor, stateOfChargeSensor, totalEnergyChargedSensor, totalEnergyDischargedSensor, maxChargePowerEntity, maxDischargePowerEntity, [], config.Timezone);

            batteries.Add(battery);
        }

        var gridVoltage = new NumericEntity(ha, config.Grid.VoltageEntity);
        var gridImport = NumericSensor.Create(ha, config.Grid.ImportEntity);
        var gridExport = NumericSensor.Create(ha, config.Grid.ExportEntity);
        var gridPeakImport = new NumericEntity(ha, config.Grid.PeakImportEntity);
        var gridCurrentAverageDemand = new NumericEntity(ha, config.Grid.CurrentAverageDemandEntity);
        var totalBatteryChargePower = NumericSensor.Create(ha, config.BatteryManager.TotalChargePowerSensor);
        var totalBatteryDischargePower = NumericSensor.Create(ha, config.BatteryManager.TotalDischargePowerSensor);
        var gridMonitor = new GridMonitor(scheduler, gridVoltage, gridImport, gridExport, gridPeakImport, gridCurrentAverageDemand, totalBatteryChargePower, totalBatteryDischargePower);

        var entity = new Domain.EnergyManager.EnergyManager(ha, logger, scheduler, mqttEntityManager, fileStorage, gridMonitor, new NumericEntity(ha, config.SolarProductionRemainingTodayEntity), consumers, batteries, phoneToNotify, TimeSpan.FromSeconds(2));
        return entity;
    }

    public static TimeWindow ToEntities(this TimeWindowConfig config, IHaContext ha)
    {
        var activeEntity = new BinarySensor(ha, config.ActiveEntity);

        return new TimeWindow(activeEntity, new TimeOnly(0, 0).Add(config.Start), new TimeOnly(0, 0).Add(config.End));
    }

}