using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests.Builders;

public class SimpleEnergyConsumer2Builder
{
    private string _name;
    private readonly List<string> _consumerGroups = [];

    private string _powerUsage;
    private string _criticallyNeeded;
    private double _switchOnLoad;
    private double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];

    private string _socket;
    private double _peakLoad;

    public SimpleEnergyConsumer2Builder(ILogger logger, AppTestContext testCtx)
    {
        _name = "Pond pump";
        _powerUsage = "sensor.socket_pond_pump_power";
        _criticallyNeeded = "boolean_sensor.weather_is_freezing";
        _switchOnLoad = -40;
        _switchOffLoad = 100;

        _socket = "switch.socket_pond_pump";
        _peakLoad = 42;
    }

    public SimpleEnergyConsumer2Builder WithName(string name)
    {
        _name = name;
        _socket = $"switch.socket_{name.MakeHaFriendly()}";
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }

    public SimpleEnergyConsumer2Builder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public SimpleEnergyConsumer2Builder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public SimpleEnergyConsumer2Builder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public SimpleEnergyConsumer2Builder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public SimpleEnergyConsumer2Builder WithLoad(double switchOnLoad, double switchOffLoad, double peakLoad)
    {
        _switchOnLoad = switchOnLoad;
        _switchOffLoad = switchOffLoad;
        _peakLoad = peakLoad;

        return this;
    }


    public SimpleEnergyConsumer2Builder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveEntity = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public EnergyConsumerConfig Build()
    {
        var x = new EnergyConsumerConfig()
        {
            Name = _name,
            ConsumerGroups = _consumerGroups,
            PowerUsageEntity = _powerUsage,
            CriticallyNeededEntity = _criticallyNeeded,
            SwitchOnLoad = _switchOnLoad,
            SwitchOffLoad = _switchOffLoad,
            MinimumRuntime = _minimumRuntime,
            MaximumRuntime = _maximumRuntime,
            MinimumTimeout = _minimumTimeout,
            MaximumTimeout = _maximumTimeout,
            TimeWindows = _timeWindows,
            Simple = new SimpleEnergyConsumerConfig
            {
                PeakLoad = _peakLoad,
                SocketEntity = _socket
            }
        };
        return x;
    }
}