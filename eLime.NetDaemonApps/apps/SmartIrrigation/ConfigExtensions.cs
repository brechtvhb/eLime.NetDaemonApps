using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.SmartIrrigation;

public static class ConfigExtensions
{

    public static Domain.SmartIrrigation.SmartIrrigation ToEntities(this SmartIrrigationConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, ILogger logger)
    {
        var availableRainWaterSensor = new NumericSensor(ha, config.AvailableRainWaterEntity);
        var minimumAvailableWater = config.MinimumAvailableRainWater;
        var pumpSocket = new BinarySwitch(ha, config.PumpSocketEntity);
        var pumpFlowRate = config.PumpFlowRate;

        var zones = new List<IrrigationZone>();
        foreach (var zone in config.Zones)
        {
            IrrigationZone irrigationZone = null;
            if (zone.Container != null)
                irrigationZone = new ContainerIrrigationZone(zone.Name, zone.FlowRate, new BinarySwitch(ha, zone.ValveEntity), new NumericSensor(ha, zone.Container.VolumeEntity), new BinarySensor(ha, zone.Container.OverFlowEntity), zone.Container.CriticalVolume, zone.Container.LowVolume, zone.Container.TargetVolume);
            if (zone.Irrigation != null)
                irrigationZone = new ClassicIrrigationZone(zone.Name, zone.FlowRate, new BinarySwitch(ha, zone.ValveEntity), new NumericSensor(ha, zone.Irrigation.SoilMoistureEntity), zone.Irrigation.CriticalSoilMoisture, zone.Irrigation.LowSoilMoisture, zone.Irrigation.TargetSoilMoisture, zone.Irrigation.MaxDuration, zone.Irrigation.MinimumTimeout, zone.Irrigation.IrrigationStartWindow, zone.Irrigation.IrrigationEndWindow);
            if (zone.AntiFrostMisting != null)
                irrigationZone = new AntiFrostMistingIrrigationZone(zone.Name, zone.FlowRate, new BinarySwitch(ha, zone.ValveEntity), new NumericSensor(ha, zone.AntiFrostMisting.TemperatureEntity), zone.AntiFrostMisting.CriticalTemperature, zone.AntiFrostMisting.LowTemperature, zone.AntiFrostMisting.MistingDuration, zone.AntiFrostMisting.MistingTimeout);

            if (irrigationZone != null)
                zones.Add(irrigationZone);
        }


        var entity = new Domain.SmartIrrigation.SmartIrrigation(ha, logger, scheduler, mqttEntityManager, pumpSocket, pumpFlowRate, availableRainWaterSensor, minimumAvailableWater, zones);
        return entity;
    }

}