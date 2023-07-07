using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Tests.Helpers;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class SimpleEnergyConsumerBuilder
{
    private readonly AppTestContext _testCtx;
    private String _name;

    private NumericEntity _powerUsage;
    private BinarySensor _criticallyNeeded;
    private Double _switchOnLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private List<TimeWindow> _timeWindows = new();

    private BinarySwitch _socket;
    private Double _peakLoad;

    public SimpleEnergyConsumerBuilder(AppTestContext testCtx)
    {
        _testCtx = testCtx;

        _name = "Pond pump";
        _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_pond_pump_power");
        _criticallyNeeded = BinarySensor.Create(_testCtx.HaContext, "boolean_sensor.weather_is_freezing");
        _switchOnLoad = -40;

        _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.socket_pond_pump");
        _peakLoad = 42;
    }

    public SimpleEnergyConsumer Build()
    {
        var x = new SimpleEnergyConsumer(_name, _powerUsage, _criticallyNeeded, _switchOnLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _socket, _peakLoad);
        return x;
    }
}