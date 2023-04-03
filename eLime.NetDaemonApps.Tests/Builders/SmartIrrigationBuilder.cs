using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
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

    public SmartIrrigationBuilder AddZone(IrrigationZone zone)
    {
        _zones.Add(zone);

        return this;
    }

    public SmartIrrigation Build()
    {

        var x = new SmartIrrigation(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _pumpSocket, _pumpFlowRate, _availableRainWaterSensor, _minimumRainWater, _zones, TimeSpan.Zero);
        return x;
    }
}