using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.SmartIrrigation;

public static class ConfigExtensions
{

    public static Domain.SmartIrrigation.SmartIrrigation ToEntities(this SmartIrrigationConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger logger)
    {
        var availableRainWaterSensor = NumericSensor.Create(ha, config.AvailableRainWaterEntity);
        var minimumAvailableWater = config.MinimumAvailableRainWater;
        var pumpSocket = BinarySwitch.Create(ha, config.PumpSocketEntity);
        var pumpFlowRate = config.PumpFlowRate;

        var weather = !String.IsNullOrWhiteSpace(config.WeatherEntity) ? new Weather(ha, config.WeatherEntity) : null;
        var predictionDays = config.RainPredictionDays;
        var predictionLiters = config.RainPredictionLiters;
        var phoneToNotify = config.PhoneToNotify;

        var zones = new List<IrrigationZone>();
        foreach (var zone in config.Zones)
        {
            IrrigationZone irrigationZone = null;
            if (zone.Container != null)
                irrigationZone = new ContainerIrrigationZone(zone.Name, zone.FlowRate, BinarySwitch.Create(ha, zone.ValveEntity), NumericSensor.Create(ha, zone.Container.VolumeEntity), BinarySensor.Create(ha, zone.Container.OverFlowEntity), zone.Container.CriticalVolume, zone.Container.LowVolume, zone.Container.TargetVolume, scheduler, zone.IrrigationSeasonStart, zone.IrrigationSeasonEnd);
            if (zone.Irrigation != null)
                irrigationZone = new ClassicIrrigationZone(zone.Name, zone.FlowRate, BinarySwitch.Create(ha, zone.ValveEntity), NumericSensor.Create(ha, zone.Irrigation.SoilMoistureEntity), zone.Irrigation.CriticalSoilMoisture, zone.Irrigation.LowSoilMoisture, zone.Irrigation.TargetSoilMoisture, zone.Irrigation.MaxDuration, zone.Irrigation.MinimumTimeout, zone.Irrigation.IrrigationStartWindow, zone.Irrigation.IrrigationEndWindow, scheduler, zone.IrrigationSeasonStart, zone.IrrigationSeasonEnd);
            if (zone.AntiFrostMisting != null)
                irrigationZone = new AntiFrostMistingIrrigationZone(zone.Name, zone.FlowRate, BinarySwitch.Create(ha, zone.ValveEntity), NumericSensor.Create(ha, zone.AntiFrostMisting.TemperatureEntity), zone.AntiFrostMisting.CriticalTemperature, zone.AntiFrostMisting.LowTemperature, zone.AntiFrostMisting.MistingDuration, zone.AntiFrostMisting.MistingTimeout, scheduler, zone.IrrigationSeasonStart, zone.IrrigationSeasonEnd);

            if (irrigationZone != null)
                zones.Add(irrigationZone);
        }


        var entity = new Domain.SmartIrrigation.SmartIrrigation(ha, logger, scheduler, mqttEntityManager, fileStorage, pumpSocket, pumpFlowRate, availableRainWaterSensor, minimumAvailableWater, weather, predictionDays, predictionLiters, phoneToNotify, zones, TimeSpan.FromSeconds(5));
        return entity;
    }

}