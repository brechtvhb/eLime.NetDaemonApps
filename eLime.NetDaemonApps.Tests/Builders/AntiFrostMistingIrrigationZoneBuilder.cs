using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests.Builders;

public class AntiFrostMistingIrrigationZoneBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private string _name;
    private int _flowRate;
    private BinarySwitch _valve;
    private NumericSensor _temperatureSensor;
    private int _lowTemperature;
    private int _criticallyLowTemperature;
    private TimeSpan _mistingDuration;
    private TimeSpan _mistingTimeout;

    private DateTimeOffset? _irrigationSeasonStart;
    private DateTimeOffset? _irrigationSeasonEnd;

    public AntiFrostMistingIrrigationZoneBuilder(AppTestContext testCtx, ILogger logger)
    {
        _testCtx = testCtx;
        _logger = logger;

        _name = "front yard";
        _flowRate = 50;
        _valve = BinarySwitch.Create(_testCtx.HaContext, "switch.fruit_trees_valve");
        _temperatureSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.fruit_trees_temperature");
        _lowTemperature = 1;
        _criticallyLowTemperature = 0;

        _mistingDuration = TimeSpan.FromMinutes(5);
        _mistingTimeout = TimeSpan.FromMinutes(5);
    }

    public AntiFrostMistingIrrigationZoneBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AntiFrostMistingIrrigationZoneBuilder WithIrrigationSeason(DateTimeOffset start, DateTimeOffset end)
    {
        _irrigationSeasonStart = start;
        _irrigationSeasonEnd = end;

        return this;
    }

    public AntiFrostMistingIrrigationZoneBuilder WithFlowRate(int flowRate)
    {
        _flowRate = flowRate;
        return this;
    }
    public AntiFrostMistingIrrigationZoneBuilder With(BinarySwitch valve)
    {
        _valve = valve;
        return this;
    }
    public AntiFrostMistingIrrigationZoneBuilder WithMistingDurations(TimeSpan maxDuration, TimeSpan minimumTimeout)
    {
        _mistingDuration = maxDuration;
        _mistingTimeout = minimumTimeout;
        return this;
    }


    public AntiFrostMistingIrrigationZoneBuilder With(NumericSensor TemperatureSensor, int lowTemperature, int criticallyLowTemperature)
    {
        _temperatureSensor = TemperatureSensor;
        _lowTemperature = lowTemperature;
        _criticallyLowTemperature = criticallyLowTemperature;

        return this;
    }


    public AntiFrostMistingIrrigationZone Build()
    {
        var x = new AntiFrostMistingIrrigationZone(_logger, _name, _flowRate, _valve, _temperatureSensor, _criticallyLowTemperature, _lowTemperature, _mistingDuration, _mistingTimeout, _testCtx.Scheduler, _irrigationSeasonStart, _irrigationSeasonEnd);
        return x;
    }
}