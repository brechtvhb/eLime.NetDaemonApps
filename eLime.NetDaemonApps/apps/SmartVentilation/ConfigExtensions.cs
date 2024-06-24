using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.SmartVentilation;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Linq;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.SmartVentilation;

public static class ConfigExtensions
{

    public static Domain.SmartVentilation.SmartVentilation ToEntities(this SmartVentilationConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage storage, ILogger logger, String netDaemonUserId)
    {
        var climate = new Climate(ha, config.ClimateEntity);

        var statePingPongGuard = config.StatePingPong.ToEntities(logger, scheduler);
        var indoorAirQualityGuard = config.Indoor.ToEntities(ha, logger, scheduler);
        var bathroomAirQualityGuard = config.Bathroom.ToEntities(ha, logger, scheduler);
        var indoorTemperatureGuard = config.IndoorTemperature.ToEntities(ha, logger, scheduler);
        var moldGuard = config.Mold.ToEntities(logger, scheduler);
        var dryAirGuard = config.DryAir.ToEntities(ha, logger, scheduler);
        var electricityBillGuard = config.ElectricityBill.ToEntities(ha, logger, scheduler);

        var ventilation = new Domain.SmartVentilation.SmartVentilation(ha, logger, scheduler, mqttEntityManager, storage, config.Enabled ?? true, climate, netDaemonUserId,
            statePingPongGuard, indoorAirQualityGuard, bathroomAirQualityGuard, indoorTemperatureGuard, moldGuard, dryAirGuard, electricityBillGuard, TimeSpan.FromSeconds(3));
        return ventilation;
    }

    public static StatePingPongGuard ToEntities(this StatePingPongGuardConfig config, ILogger logger, IScheduler scheduler)
    {

        var guard = new StatePingPongGuard(logger, scheduler, config.TimeoutSpan);
        return guard;
    }
    public static IndoorAirQualityGuard ToEntities(this IndoorAirQualityGuardConfig config, IHaContext ha, ILogger logger, IScheduler scheduler)
    {
        var co2Sensors = config.Co2Sensors.Select(x => new NumericSensor(ha, x)).ToList();
        var guard = new IndoorAirQualityGuard(logger, scheduler, co2Sensors, config.Co2MediumThreshold, config.Co2HighThreshold);
        return guard;
    }
    public static BathroomAirQualityGuard ToEntities(this BathroomAirQualityGuardConfig config, IHaContext ha, ILogger logger, IScheduler scheduler)
    {
        var humiditySensors = config.HumiditySensors.Select(x => new NumericSensor(ha, x)).ToList();
        var guard = new BathroomAirQualityGuard(logger, scheduler, humiditySensors, config.HumidityMediumThreshold, config.HumidityHighThreshold);
        return guard;
    }
    public static IndoorTemperatureGuard ToEntities(this IndoorTemperatureGuardConfig config, IHaContext ha, ILogger logger, IScheduler scheduler)
    {
        var summerModeSensor = new BinarySensor(ha, config.SummerModeSensor);
        var outdoorTemperatureSensor = new NumericSensor(ha, config.OutdoorTemperatureSensor);
        var postHeatExchangerTemperatureSensor = new NumericSensor(ha, config.PostHeatExchangerTemperatureSensor);
        var guard = new IndoorTemperatureGuard(logger, scheduler, summerModeSensor, outdoorTemperatureSensor, postHeatExchangerTemperatureSensor);
        return guard;
    }
    public static MoldGuard ToEntities(this MoldGuardConfig config, ILogger logger, IScheduler scheduler)
    {
        var guard = new MoldGuard(logger, scheduler, config.MaxAwayTimeSpan, config.RechargeTimeSpan);
        return guard;
    }
    public static DryAirGuard ToEntities(this DryAirGuardConfig config, IHaContext ha, ILogger logger, IScheduler scheduler)
    {
        var humiditySensors = config.HumiditySensors.Select(x => new NumericSensor(ha, x)).ToList();
        var outdoorTemperatureSensor = new NumericSensor(ha, config.OutdoorTemperatureSensor);

        var guard = new DryAirGuard(logger, scheduler, humiditySensors, config.HumidityLowThreshold, outdoorTemperatureSensor, config.MaxOutdoorTemperature);
        return guard;
    }

    public static ElectricityBillGuard ToEntities(this ElectricityBillGuardConfig config, IHaContext ha, ILogger logger, IScheduler scheduler)
    {
        var awaySensor = new BinarySensor(ha, config.AwaySensor);
        var sleepingSensor = new BinarySensor(ha, config.SleepingSensor);

        var guard = new ElectricityBillGuard(logger, scheduler, awaySensor, sleepingSensor);
        return guard;
    }
}