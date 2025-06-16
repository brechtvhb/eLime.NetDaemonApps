using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;

namespace eLime.NetDaemonApps.Tests.Builders;

public class CoolingEnergyConsumer2Builder
{
    private string _name = "Fridge";
    private List<string> _consumerGroups = [];

    private string _powerUsage = "sensor.socket_fridge_power";
    private string? _criticallyNeeded;
    private double _switchOnLoad = -50;
    private double _switchOffLoad = 200;

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

    public CoolingEnergyConsumer2Builder()
    {
        AddConsumerGroup("Critical");
    }

    public CoolingEnergyConsumer2Builder WithName(string name)
    {
        _name = name;
        _socket = $"switch.socket_{name.MakeHaFriendly()}";
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }

    public CoolingEnergyConsumer2Builder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public CoolingEnergyConsumer2Builder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public CoolingEnergyConsumer2Builder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public CoolingEnergyConsumer2Builder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public CoolingEnergyConsumer2Builder WithLoad(double switchOnLoad, double switchOffLoad, double peakLoad)
    {
        _switchOnLoad = switchOnLoad;
        _peakLoad = peakLoad;

        return this;
    }


    public CoolingEnergyConsumer2Builder AddTimeWindow(string? isActiveSensor, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActiveSensor,
            Start = start,
            End = end
        });
        return this;
    }

    public CoolingEnergyConsumer2Builder WithTemperatureSensor(string sensorName, double targetTemperature, double maxTemperature)
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