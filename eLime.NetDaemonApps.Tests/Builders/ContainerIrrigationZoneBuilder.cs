﻿using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests.Builders;

public class ContainerIrrigationZoneBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private string _name;
    private int _flowRate;
    private BinarySwitch _valve;
    private NumericSensor _volumeSensor;
    private BinarySensor _overflowSensor;
    private int _lowVolume;
    private int _criticallyLowVolume;
    private int _targetVolume;

    private DateTimeOffset? _irrigationSeasonStart;
    private DateTimeOffset? _irrigationSeasonEnd;

    public ContainerIrrigationZoneBuilder(AppTestContext testCtx, ILogger logger)
    {
        _testCtx = testCtx;
        _logger = logger;

        _name = "pond";
        _flowRate = 500;
        _valve = BinarySwitch.Create(_testCtx.HaContext, "switch.pond_valve");
        _volumeSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.pond_volume");
        _overflowSensor = BinarySensor.Create(_testCtx.HaContext, "binary_sensor.pond_overflow");
        _lowVolume = 6000;
        _criticallyLowVolume = 5000;
        _targetVolume = 7500;
    }

    public ContainerIrrigationZoneBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ContainerIrrigationZoneBuilder WithIrrigationSeason(DateTimeOffset start, DateTimeOffset end)
    {
        _irrigationSeasonStart = start;
        _irrigationSeasonEnd = end;

        return this;
    }

    public ContainerIrrigationZoneBuilder WithFlowRate(int flowRate)
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

    public ContainerIrrigationZoneBuilder With(NumericSensor volumeSensor, int lowVolume, int criticallyLowVolume, int targetVolume)
    {
        _volumeSensor = volumeSensor;
        _lowVolume = lowVolume;
        _criticallyLowVolume = criticallyLowVolume;
        _targetVolume = targetVolume;

        return this;
    }


    public ContainerIrrigationZone Build()
    {
        var x = new ContainerIrrigationZone(_logger, _name, _flowRate, _valve, _volumeSensor, _overflowSensor, _criticallyLowVolume, _lowVolume, _targetVolume, _testCtx.Scheduler, _irrigationSeasonStart, _irrigationSeasonEnd);
        return x;
    }
}