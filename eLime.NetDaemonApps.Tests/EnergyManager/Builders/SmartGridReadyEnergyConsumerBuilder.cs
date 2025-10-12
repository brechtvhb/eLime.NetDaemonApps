using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;
#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class SmartGridReadyEnergyConsumerBuilder
{
    private string _name;
    private readonly List<string> _consumerGroups = [];

    private string _powerConsumptionSensor;
    private string _expectedPeakLoadSensor;
    private double _fallbackPeakLoad;
    private double _switchOnLoad;
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStart = [];
    private double _switchOffLoad;
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStop = [];
    private LoadTimeFrames _loadTimeFrameToCheckOnRebalance;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];
    private readonly List<TimeWindowConfig> _blockedTimeWindows = [];

    private string _smartGridModeSelectEntity;
    private string _stateSensor;
    private string _energyNeededState;
    private string _criticalEnergyNeededState;

    public SmartGridReadyEnergyConsumerBuilder()
    {
    }

    public static SmartGridReadyEnergyConsumerBuilder HeatPump =>
        new SmartGridReadyEnergyConsumerBuilder()
            .WithName("Heat pump")
            .WithLoad(-1200, [LoadTimeFrames.Last30Seconds], 2000, [LoadTimeFrames.Last30Seconds], LoadTimeFrames.Last30Seconds, "sensor.heat_pump_expected_power_consumption", 1700)
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.heat_pump_state", "Demanded", "CriticalDemand");

    public SmartGridReadyEnergyConsumerBuilder WithName(string name)
    {
        _name = name;
        _smartGridModeSelectEntity = $"select.{name.MakeHaFriendly()}_smartgrid_mode";
        _powerConsumptionSensor = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }

    public SmartGridReadyEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }


    public SmartGridReadyEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public SmartGridReadyEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public SmartGridReadyEnergyConsumerBuilder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public SmartGridReadyEnergyConsumerBuilder AddBlockedTimeWindow(string? isActive, List<DayOfWeek> days, TimeSpan start, TimeSpan end)
    {
        _blockedTimeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Days = days,
            Start = start,
            End = end
        });
        return this;
    }


    public SmartGridReadyEnergyConsumerBuilder WithLoad(double switchOn, List<LoadTimeFrames> loadTimeFramesToCheckOnStart, double switchOff, List<LoadTimeFrames> loadTimeFramesToCheckOnStop, LoadTimeFrames loadTimeFrameToCheckOnRebalance, string expectedPeakLoadSensor, double peakLoad)
    {
        _switchOnLoad = switchOn;
        _loadTimeFramesToCheckOnStart = loadTimeFramesToCheckOnStart;
        _switchOffLoad = switchOff;
        _loadTimeFramesToCheckOnStop = loadTimeFramesToCheckOnStop;

        _expectedPeakLoadSensor = expectedPeakLoadSensor;
        _fallbackPeakLoad = peakLoad;
        _loadTimeFrameToCheckOnRebalance = loadTimeFrameToCheckOnRebalance;

        return this;
    }

    public SmartGridReadyEnergyConsumerBuilder WithStateSensor(string stateSensor, string energyNeededState, string criticalEnergyNeededState)
    {
        _stateSensor = stateSensor;
        _energyNeededState = energyNeededState;
        _criticalEnergyNeededState = criticalEnergyNeededState;
        return this;
    }

    public EnergyConsumerConfig Build()
    {
        return new EnergyConsumerConfig
        {
            Name = _name,
            PowerUsageEntity = _powerConsumptionSensor,
            ConsumerGroups = _consumerGroups,
            SwitchOnLoad = _switchOnLoad,
            SwitchOffLoad = _switchOffLoad,
            LoadTimeFramesToCheckOnStart = _loadTimeFramesToCheckOnStart,
            LoadTimeFramesToCheckOnStop = _loadTimeFramesToCheckOnStop,
            LoadTimeFrameToCheckOnRebalance = _loadTimeFrameToCheckOnRebalance,
            MinimumRuntime = _minimumRuntime,
            MaximumRuntime = _maximumRuntime,
            MinimumTimeout = _minimumTimeout,
            MaximumTimeout = _maximumTimeout,
            TimeWindows = _timeWindows,

            SmartGridReady = new SmartGridReadyEnergyConsumerConfig
            {
                StateSensor = _stateSensor,
                EnergyNeededState = _energyNeededState,
                CriticalEnergyNeededState = _criticalEnergyNeededState,
                ExpectedPeakLoadSensor = _expectedPeakLoadSensor,
                FallbackPeakLoad = _fallbackPeakLoad,
                BlockedTimeWindows = _blockedTimeWindows,
                SmartGridModeEntity = _smartGridModeSelectEntity
            }
        };
    }
}