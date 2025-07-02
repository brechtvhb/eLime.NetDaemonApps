using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class SimpleEnergyConsumerBuilder
{
    private string _name = "Pond pump";
    private readonly List<string> _consumerGroups = [];

    private string _powerUsage = "sensor.socket_pond_pump_power";
    private string _criticallyNeeded = "boolean_sensor.weather_is_freezing";
    private double _switchOnLoad = -40;
    private double _switchOffLoad = 100;
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStart = [];
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStop = [];

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];

    private string _socket = "switch.socket_pond_pump";
    private double _peakLoad = 42;

    public SimpleEnergyConsumerBuilder WithName(string name)
    {
        _name = name;
        _socket = $"switch.socket_{name.MakeHaFriendly()}";
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }

    public SimpleEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public SimpleEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public SimpleEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public SimpleEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public SimpleEnergyConsumerBuilder WithLoad(double switchOn, List<LoadTimeFrames> loadTimeFramesToCheckOnStart, double switchOff, List<LoadTimeFrames> loadTimeFramesToCheckOnStop, double peakLoad)
    {
        _switchOnLoad = switchOn;
        _loadTimeFramesToCheckOnStart = loadTimeFramesToCheckOnStart;
        _switchOffLoad = switchOff;
        _loadTimeFramesToCheckOnStop = loadTimeFramesToCheckOnStop;
        _peakLoad = peakLoad;

        return this;
    }

    public SimpleEnergyConsumerBuilder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public static SimpleEnergyConsumerBuilder PondPump()
    {
        return new SimpleEnergyConsumerBuilder()
            .WithLoad(-40, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 42);
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
            LoadTimeFramesToCheckOnStart = _loadTimeFramesToCheckOnStart,
            LoadTimeFramesToCheckOnStop = _loadTimeFramesToCheckOnStop,
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