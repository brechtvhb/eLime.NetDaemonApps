using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Tests.Helpers;

namespace eLime.NetDaemonApps.Tests.Builders;

public class AntiFrostMistingIrrigationZoneBuilder
{
    private readonly AppTestContext _testCtx;
    private String _name;
    private Int32 _flowRate;
    private BinarySwitch _valve;
    private NumericSensor _temperatureSensor;
    private Int32 _lowTemperature;
    private Int32 _criticallyLowTemperature;
    private TimeSpan _mistingDuration;
    private TimeSpan _mistingTimeout;

    private DateTimeOffset? _irrigationSeasonStart;
    private DateTimeOffset? _irrigationSeasonEnd;

    public AntiFrostMistingIrrigationZoneBuilder(AppTestContext testCtx)
    {
        _testCtx = testCtx;

        _name = "front yard";
        _flowRate = 50;
        _valve = BinarySwitch.Create(_testCtx.HaContext, "switch.fruit_trees_valve");
        _temperatureSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.fruit_trees_temperature");
        _lowTemperature = 1;
        _criticallyLowTemperature = 0;

        _mistingDuration = TimeSpan.FromMinutes(5);
        _mistingTimeout = TimeSpan.FromMinutes(5);
    }

    public AntiFrostMistingIrrigationZoneBuilder WithName(String name)
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

    public AntiFrostMistingIrrigationZoneBuilder WithFlowRate(Int32 flowRate)
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


    public AntiFrostMistingIrrigationZoneBuilder With(NumericSensor TemperatureSensor, Int32 lowTemperature, Int32 criticallyLowTemperature)
    {
        _temperatureSensor = TemperatureSensor;
        _lowTemperature = lowTemperature;
        _criticallyLowTemperature = criticallyLowTemperature;

        return this;
    }


    public AntiFrostMistingIrrigationZone Build()
    {
        var x = new AntiFrostMistingIrrigationZone(_name, _flowRate, _valve, _temperatureSensor, _criticallyLowTemperature, _lowTemperature, _mistingDuration, _mistingTimeout, _testCtx.Scheduler, _irrigationSeasonStart, _irrigationSeasonEnd);
        return x;
    }
}