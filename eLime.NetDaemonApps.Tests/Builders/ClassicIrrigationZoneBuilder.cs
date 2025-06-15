using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests.Builders;

public class ClassicIrrigationZoneBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private string _name;
    private int _flowRate;
    private BinarySwitch _valve;
    private NumericSensor _soilMoistureSensor;
    private int _lowSoilMoisture;
    private int _criticallyLowSoilMoisture;
    private int _targetSoilMoisture;
    private TimeSpan? _maxDuration;
    private TimeSpan? _minimumTimeout;
    private TimeOnly? _startWindow;
    private TimeOnly? _endWindow;

    private DateTimeOffset? _irrigationSeasonStart;
    private DateTimeOffset? _irrigationSeasonEnd;

    public ClassicIrrigationZoneBuilder(AppTestContext testCtx, ILogger logger)
    {
        _testCtx = testCtx;
        _logger = logger;

        _name = "front yard";
        _flowRate = 1500;
        _valve = BinarySwitch.Create(_testCtx.HaContext, "switch.front_yard_valve");
        _soilMoistureSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.front_yard_soil_moisture");
        _lowSoilMoisture = 35;
        _criticallyLowSoilMoisture = 30;
        _targetSoilMoisture = 45;
    }

    public ClassicIrrigationZoneBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ClassicIrrigationZoneBuilder WithIrrigationSeason(DateTimeOffset start, DateTimeOffset end)
    {
        _irrigationSeasonStart = start;
        _irrigationSeasonEnd = end;

        return this;
    }

    public ClassicIrrigationZoneBuilder WithFlowRate(int flowRate)
    {
        _flowRate = flowRate;
        return this;
    }
    public ClassicIrrigationZoneBuilder With(BinarySwitch valve)
    {
        _valve = valve;
        return this;
    }
    public ClassicIrrigationZoneBuilder WithMaxDuration(TimeSpan? maxDuration, TimeSpan? minimumTimeout)
    {
        _maxDuration = maxDuration;
        _minimumTimeout = minimumTimeout;
        return this;
    }

    public ClassicIrrigationZoneBuilder WithTimeWindow(TimeOnly? start, TimeOnly? end)
    {
        _startWindow = start;
        _endWindow = end;
        return this;
    }


    public ClassicIrrigationZoneBuilder With(NumericSensor soilMoistureSensor, int lowSoilMoisture, int criticallyLowSoilMoisture, int targetSoilMoisture)
    {
        _soilMoistureSensor = soilMoistureSensor;
        _lowSoilMoisture = lowSoilMoisture;
        _criticallyLowSoilMoisture = criticallyLowSoilMoisture;
        _targetSoilMoisture = targetSoilMoisture;

        return this;
    }


    public ClassicIrrigationZone Build()
    {
        var x = new ClassicIrrigationZone(_logger, _name, _flowRate, _valve, _soilMoistureSensor, _criticallyLowSoilMoisture, _lowSoilMoisture, _targetSoilMoisture, _maxDuration, _minimumTimeout, _startWindow, _endWindow, _testCtx.Scheduler, _irrigationSeasonStart, _irrigationSeasonEnd);
        return x;
    }
}