using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Tests.Builders;

public class VentilationBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private readonly IScheduler _scheduler;

    private Climate _climate;
    private StatePingPongGuard _statePingPongGuard;
    private IndoorAirQualityGuard _indoorAirQualityGuard;
    private BathroomAirQualityGuard _bathroomAirQualityGuard;
    private MoldGuard _moldGuard;
    private DryAirGuard _dryAirGuard;
    private ElectricityBillGuard _electricityBillGuard;

    public VentilationBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IScheduler scheduler)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _scheduler = scheduler;

        _statePingPongGuard = new StatePingPongGuard(_logger, _scheduler, TimeSpan.FromMinutes(30));
        _indoorAirQualityGuard = new IndoorAirQualityGuard(_logger, _scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.co2") }, 850, 1000);
        _bathroomAirQualityGuard = new BathroomAirQualityGuard(_logger, _scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.humidity_bathroom") }, 70, 80);
        _moldGuard = new MoldGuard(_logger, _scheduler, TimeSpan.FromHours(10), TimeSpan.FromHours(1));
        _dryAirGuard = new DryAirGuard(_logger, _scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.humidity_living") }, 38, new NumericSensor(testCtx.HaContext, "sensor.outdoor_temp"), 10);
        _electricityBillGuard = new ElectricityBillGuard(_logger, _scheduler, new BinarySensor(_testCtx.HaContext, "binary_sensor.away"), new BinarySensor(_testCtx.HaContext, "binary_sensor.sleeping"));
    }

    public VentilationBuilder With(Climate climate)
    {
        _climate = climate;
        return this;
    }

    public VentilationBuilder WithStatePingPongGuard(TimeSpan timeoutSpan)
    {
        _statePingPongGuard = new StatePingPongGuard(_logger, _scheduler, timeoutSpan);
        return this;
    }
    public VentilationBuilder WithIndoorAirQualityGuard(List<NumericSensor> co2Sensors, Int32 co2MediumThreshold, Int32 co2HighThreshold)
    {
        _indoorAirQualityGuard = new IndoorAirQualityGuard(_logger, _scheduler, co2Sensors, co2MediumThreshold, co2HighThreshold);
        return this;
    }
    public VentilationBuilder WithBathroomAirQualityGuard(List<NumericSensor> humiditySensors, Int32 humidityMediumThreshold, Int32 humidityHighThreshold)
    {
        _bathroomAirQualityGuard = new BathroomAirQualityGuard(_logger, _scheduler, humiditySensors, humidityMediumThreshold, humidityHighThreshold);
        return this;
    }
    public VentilationBuilder WithMoldGuard(TimeSpan? maxAwayTimespan, TimeSpan? rechargeTimespan)
    {
        _moldGuard = new MoldGuard(_logger, _scheduler, maxAwayTimespan, rechargeTimespan);
        return this;
    }
    public VentilationBuilder WithDryAirGuard(List<NumericSensor> indoorHumiditySensors, Int32 lowHumidityThreshold, NumericSensor outdoorTemperatureSensor, Int32 maxOutdoorTemperature)
    {
        _dryAirGuard = new DryAirGuard(_logger, _scheduler, indoorHumiditySensors, lowHumidityThreshold, outdoorTemperatureSensor, maxOutdoorTemperature);
        return this;
    }
    public VentilationBuilder WithElectricityBillGuard(BinarySensor awaySensor, BinarySensor sleepSensor)
    {
        _electricityBillGuard = new ElectricityBillGuard(_logger, _scheduler, awaySensor, sleepSensor);
        return this;
    }


    public SmartVentilation Build()
    {

        var ventilation = new SmartVentilation(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _fileStorage, true, _climate, "somecoolid",
            _statePingPongGuard, _indoorAirQualityGuard, _bathroomAirQualityGuard, _moldGuard, _dryAirGuard, _electricityBillGuard, TimeSpan.Zero);

        return ventilation;
    }
}