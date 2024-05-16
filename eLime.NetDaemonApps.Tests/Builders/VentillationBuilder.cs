using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests.Builders;

public class VentilationBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private Climate _climate;
    private StatePingPongGuard _statePingPongGuard;
    private IndoorAirQualityGuard _indoorAirQualityGuard;
    private BathroomAirQualityGuard _bathroomAirQualityGuard;
    private IndoorTemperatureGuard _indoorTemperatureGuard;
    private MoldGuard _moldGuard;
    private DryAirGuard _dryAirGuard;
    private ElectricityBillGuard _electricityBillGuard;

    public VentilationBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        _statePingPongGuard = new StatePingPongGuard(_logger, _testCtx.Scheduler, TimeSpan.FromMinutes(30));
        _indoorAirQualityGuard = new IndoorAirQualityGuard(_logger, _testCtx.Scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.co2") }, 850, 1000);
        _bathroomAirQualityGuard = new BathroomAirQualityGuard(_logger, _testCtx.Scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.humidity_bathroom") }, 70, 80);
        _indoorTemperatureGuard = new IndoorTemperatureGuard(_logger, _testCtx.Scheduler, new BinarySensor(_testCtx.HaContext, "binary_sensor.summer"), new NumericSensor(testCtx.HaContext, "sensor.outdoor_temp"));
        _moldGuard = new MoldGuard(_logger, _testCtx.Scheduler, TimeSpan.FromHours(10), TimeSpan.FromHours(1));
        _dryAirGuard = new DryAirGuard(_logger, _testCtx.Scheduler, new List<NumericSensor> { new(testCtx.HaContext, "sensor.humidity_living") }, 38, new NumericSensor(testCtx.HaContext, "sensor.outdoor_temp"), 10);
        _electricityBillGuard = new ElectricityBillGuard(_logger, _testCtx.Scheduler, new BinarySensor(_testCtx.HaContext, "binary_sensor.away"), new BinarySensor(_testCtx.HaContext, "binary_sensor.sleeping"));
    }

    public VentilationBuilder With(Climate climate)
    {
        _climate = climate;
        return this;
    }

    public VentilationBuilder WithStatePingPongGuard(TimeSpan timeoutSpan)
    {
        _statePingPongGuard = new StatePingPongGuard(_logger, _testCtx.Scheduler, timeoutSpan);
        return this;
    }
    public VentilationBuilder WithIndoorAirQualityGuard(List<NumericSensor> co2Sensors, Int32 co2MediumThreshold, Int32 co2HighThreshold)
    {
        _indoorAirQualityGuard = new IndoorAirQualityGuard(_logger, _testCtx.Scheduler, co2Sensors, co2MediumThreshold, co2HighThreshold);
        return this;
    }
    public VentilationBuilder WithBathroomAirQualityGuard(List<NumericSensor> humiditySensors, Int32 humidityMediumThreshold, Int32 humidityHighThreshold)
    {
        _bathroomAirQualityGuard = new BathroomAirQualityGuard(_logger, _testCtx.Scheduler, humiditySensors, humidityMediumThreshold, humidityHighThreshold);
        return this;
    }
    public VentilationBuilder WithIndoorTemperatureGuard(BinarySensor summerModeSensor, NumericSensor outdoorTemperatureSensor)
    {
        _indoorTemperatureGuard = new IndoorTemperatureGuard(_logger, _testCtx.Scheduler, summerModeSensor, outdoorTemperatureSensor);
        return this;
    }
    public VentilationBuilder WithMoldGuard(TimeSpan? maxAwayTimespan, TimeSpan? rechargeTimespan)
    {
        _moldGuard = new MoldGuard(_logger, _testCtx.Scheduler, maxAwayTimespan, rechargeTimespan);
        return this;
    }
    public VentilationBuilder WithDryAirGuard(List<NumericSensor> indoorHumiditySensors, Int32 lowHumidityThreshold, NumericSensor outdoorTemperatureSensor, Int32 maxOutdoorTemperature)
    {
        _dryAirGuard = new DryAirGuard(_logger, _testCtx.Scheduler, indoorHumiditySensors, lowHumidityThreshold, outdoorTemperatureSensor, maxOutdoorTemperature);
        return this;
    }
    public VentilationBuilder WithElectricityBillGuard(BinarySensor awaySensor, BinarySensor sleepSensor)
    {
        _electricityBillGuard = new ElectricityBillGuard(_logger, _testCtx.Scheduler, awaySensor, sleepSensor);
        return this;
    }


    public SmartVentilation Build()
    {

        var ventilation = new SmartVentilation(_testCtx.HaContext, _logger, _testCtx.Scheduler, _mqttEntityManager, _fileStorage, true, _climate, "somecoolid",
            _statePingPongGuard, _indoorAirQualityGuard, _bathroomAirQualityGuard, _indoorTemperatureGuard, _moldGuard, _dryAirGuard, _electricityBillGuard, TimeSpan.Zero);

        return ventilation;
    }
}