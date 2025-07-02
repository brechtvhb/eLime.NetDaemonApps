using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class CoolingEnergyConsumerBuilder
{
    private string _name = "Fridge";
    private List<string> _consumerGroups = [];

    private string _powerUsage = "sensor.socket_fridge_power";
    private string? _criticallyNeeded;
    private double _switchOnLoad = -50;
    private double _switchOffLoad = 200;
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStart = [];
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStop = [];

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];

    private string _socket = "switch.socket_fridge";
    private double _peakLoad = 75;

    private string _temperatureSensor = "sensor.fridge_temperature";
    private double _targetTemperature = 0.5;
    private double _maxTemperature = 8;

    public CoolingEnergyConsumerBuilder()
    {
        AddConsumerGroup("Critical");
    }

    public CoolingEnergyConsumerBuilder WithName(string name)
    {
        _name = name;
        _socket = $"switch.socket_{name.MakeHaFriendly()}";
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }

    public CoolingEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public CoolingEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public CoolingEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public CoolingEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public CoolingEnergyConsumerBuilder WithLoad(double switchOn, List<LoadTimeFrames> loadTimeFramesToCheckOnStart, double switchOff, List<LoadTimeFrames> loadTimeFramesToCheckOnStop, double peakLoad)
    {
        _switchOnLoad = switchOn;
        _loadTimeFramesToCheckOnStart = loadTimeFramesToCheckOnStart;
        _switchOffLoad = switchOff;
        _loadTimeFramesToCheckOnStop = loadTimeFramesToCheckOnStop;
        _peakLoad = peakLoad;

        return this;
    }


    public CoolingEnergyConsumerBuilder AddTimeWindow(string? isActiveSensor, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActiveSensor,
            Start = start,
            End = end
        });
        return this;
    }

    public CoolingEnergyConsumerBuilder WithTemperatureSensor(string sensorName, double targetTemperature, double maxTemperature)
    {
        _temperatureSensor = sensorName;
        _targetTemperature = targetTemperature;
        _maxTemperature = maxTemperature;

        return this;
    }

    public EnergyConsumerConfig Build()
    {
        var x = new EnergyConsumerConfig
        {
            Name = _name,
            ConsumerGroups = _consumerGroups,
            PowerUsageEntity = _powerUsage,
            SwitchOnLoad = _switchOnLoad,
            SwitchOffLoad = _switchOffLoad,
            LoadTimeFramesToCheckOnStart = _loadTimeFramesToCheckOnStart,
            LoadTimeFramesToCheckOnStop = _loadTimeFramesToCheckOnStop,
            MinimumRuntime = _minimumRuntime,
            MaximumRuntime = _maximumRuntime,
            MinimumTimeout = _minimumTimeout,
            MaximumTimeout = _maximumTimeout,
            CriticallyNeededEntity = _criticallyNeeded,
            TimeWindows = _timeWindows,
            Cooling = new CoolingEnergyConsumerConfig
            {
                SocketEntity = _socket,
                PeakLoad = _peakLoad,
                TemperatureSensor = _temperatureSensor,
                TargetTemperature = _targetTemperature,
                MaxTemperature = _maxTemperature
            }
        };

        return x;
    }
}