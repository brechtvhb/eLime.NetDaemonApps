using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Tests.Builders;

public class SmartIrrigationBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IScheduler _scheduler;

    private BinarySwitch _pumpSocket;
    private Int32 _pumpFlowRate;

    private NumericSensor _availableRainWaterSensor;
    private Int32 _minimumRainWater;

    private Weather? _weather;
    private Int32? _predictionDays;
    private Double? _predictedLiters;
    private String? _phoneToNotify;

    private List<IrrigationZone> _zones;

    public SmartIrrigationBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IScheduler scheduler)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _scheduler = scheduler;

        _pumpSocket = BinarySwitch.Create(_testCtx.HaContext, "switch.rainwater_pump");
        _pumpFlowRate = 2000;

        _availableRainWaterSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.rainwater_volume");
        _minimumRainWater = 1000;

        _phoneToNotify = "brecht";

        _zones = new List<IrrigationZone> { };
    }

    public SmartIrrigationBuilder With(BinarySwitch pumpSocket, Int32 pumpFlowRate)
    {
        _pumpSocket = pumpSocket;
        _pumpFlowRate = pumpFlowRate;

        return this;
    }

    public SmartIrrigationBuilder With(NumericSensor availableRainWaterSensor, Int32 minimumRainWater)
    {
        _availableRainWaterSensor = availableRainWaterSensor;
        _minimumRainWater = minimumRainWater;

        return this;
    }

    public SmartIrrigationBuilder With(Weather weather, Int32 predictionDays, Double predictedLiters)
    {
        _weather = weather;
        _predictionDays = predictionDays;
        _predictedLiters = predictedLiters;

        return this;
    }

    public SmartIrrigationBuilder AddZone(IrrigationZone zone)
    {
        _zones.Add(zone);

        return this;
    }

    public SmartIrrigation Build()
    {

        var x = new SmartIrrigation(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _pumpSocket, _pumpFlowRate, _availableRainWaterSensor, _minimumRainWater, _weather, _predictionDays, _predictedLiters, _phoneToNotify, _zones, TimeSpan.Zero);
        return x;
    }
}