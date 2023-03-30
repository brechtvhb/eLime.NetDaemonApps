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

        var x = new SmartIrrigation(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _pumpSocket, _pumpFlowRate, _availableRainWaterSensor, _minimumRainWater, _zones);
        return x;
    }
}

public class ContainerIrrigationZoneBuilder
{
    private readonly AppTestContext _testCtx;
    private String _name;
    private Int32 _flowRate;
    private BinarySwitch _valve;
    private NumericSensor _volumeSensor;
    private BinarySensor _overflowSensor;
    private Int32 _lowVolume;
    private Int32 _criticallyLowVolume;
    private Int32 _targetVolume;

    public ContainerIrrigationZoneBuilder(AppTestContext testCtx)
    {
        _testCtx = testCtx;

        _name = "pond";
        _flowRate = 500;
        _valve = BinarySwitch.Create(_testCtx.HaContext, "switch.pond_valve");
        _volumeSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.pond_volume");
        _overflowSensor = BinarySensor.Create(_testCtx.HaContext, "binary_sensor.pond_overflow");
        _lowVolume = 6000;
        _criticallyLowVolume = 5000;
        _targetVolume = 7500;
    }

    public ContainerIrrigationZoneBuilder WithName(String name)
    {
        _name = name;
        return this;
    }

    public ContainerIrrigationZoneBuilder WithFlowRate(Int32 flowRate)
    {
        _flowRate = flowRate;
        return this;
    }
    public ContainerIrrigationZoneBuilder With(BinarySwitch valve)
    {
        _valve = valve;
        return this;
    }
    public ContainerIrrigationZoneBuilder With(BinarySensor overflowSensor)
    {
        _overflowSensor = overflowSensor;
        return this;
    }

    public ContainerIrrigationZoneBuilder With(NumericSensor volumeSensor, Int32 lowVolume, Int32 criticallyLowVolume, Int32 targetVolume)
    {
        _volumeSensor = volumeSensor;
        _lowVolume = lowVolume;
        _criticallyLowVolume = criticallyLowVolume;
        _targetVolume = targetVolume;

        return this;
    }


    public ContainerIrrigationZone Build()
    {
        var x = new ContainerIrrigationZone(_name, _flowRate, _valve, _volumeSensor, _overflowSensor, _criticallyLowVolume, _lowVolume, _targetVolume);
        return x;
    }
}