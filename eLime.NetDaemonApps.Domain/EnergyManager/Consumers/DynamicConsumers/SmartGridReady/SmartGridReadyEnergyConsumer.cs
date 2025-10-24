using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.SmartHeatPump;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.SmartGridReady;

public class SmartGridReadyEnergyConsumer : EnergyConsumer, IDynamicLoadConsumer
{
    public BalancingMethod BalancingMethod => State.BalancingMethod ?? BalancingMethod.SolarOnly;
    public string BalanceOnBehalfOf => State.BalanceOnBehalfOf ?? IDynamicLoadConsumer.CONSUMER_GROUP_SELF;
    public AllowBatteryPower AllowBatteryPower => State.AllowBatteryPower ?? AllowBatteryPower.No;
    public double ReleasablePowerWhenBalancingOnBehalfOf => HomeAssistant.StateSensor.State == CanUseExcessEnergyState
        ? HomeAssistant.PowerConsumptionSensor.State ?? 0
        : 0;
    public bool ApplyExpectedLoadCorrections => true;

    public List<DynamicEnergyConsumerBalancingMethodBasedLoads> DynamicBalancingMethodBasedLoads { get; }
    internal override double SwitchOnLoad => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.SwitchOnLoad ?? _switchOnLoad
        : _switchOnLoad;

    internal override double SwitchOffLoad => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.SwitchOffLoad ?? _switchOffLoad
        : _switchOffLoad;

    private LoadTimeFrames _loadTimeFrameToCheckOnRebalance { get; init; }

    internal LoadTimeFrames LoadTimeFrameToCheckOnRebalance => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.LoadTimeFrameToCheckOnRebalance ?? _loadTimeFrameToCheckOnRebalance
        : _loadTimeFrameToCheckOnRebalance;

    internal sealed override DynamicEnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override SmartGridReadyEnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal override bool IsRunning => HomeAssistant.PowerConsumptionSensor.State > 100 || SmartGridReadyMode == SmartGridReadyMode.Boosted;
    internal SmartGridReadyMode SmartGridReadyMode => HomeAssistant.SmartGridModeSelect.State != null
        ? Enum<SmartGridReadyMode>.Cast(HomeAssistant.SmartGridModeSelect.State)
        : SmartGridReadyMode.Normal;

    private readonly double _fallbackPeakLoad;
    internal override double PeakLoad => HomeAssistant.ExpectedPeakLoadSensor.State is > 0
        ? HomeAssistant.ExpectedPeakLoadSensor.State.Value
        : _fallbackPeakLoad;

    internal string CanUseExcessEnergyState { get; }
    internal string EnergyNeededState { get; }
    internal string CriticalEnergyNeededState { get; }
    internal List<TimeWindow> BlockedTimeWindows { get; set; }
    protected IDisposable? StateWatcherTask { get; set; }
    private DateTimeOffset? EndedBoostAt { get; set; }
    private DateTimeOffset? LastSmartGridReadyChangedAt { get; set; }

    internal SmartGridReadyEnergyConsumer(EnergyManagerContext context, EnergyConsumerConfiguration config)
        : base(context, config)
    {
        if (config.SmartGridReady == null)
            throw new ArgumentException("Smart grid ready configuration is required for SmartGridReadyEnergyConsumer.");

        HomeAssistant = new SmartGridReadyEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.StateSensor.StateChanged += StateSensor_StateChanged;
        HomeAssistant.SmartGridModeSelect.Changed += SmartGridModeSelect_Changed;
        MqttSensors = new DynamicEnergyConsumerMqttSensors(config.Name, context);
        MqttSensors.BalancingMethodChanged += BalancingMethodChanged;
        MqttSensors.BalanceOnBehalfOfChanged += BalanceOnBehalfOfChanged;
        MqttSensors.AllowBatteryPowerChanged += AllowBatteryPowerChanged;

        _fallbackPeakLoad = config.SmartGridReady.FallbackPeakLoad;
        DynamicBalancingMethodBasedLoads = config.DynamicBalancingMethodBasedLoads;
        _loadTimeFrameToCheckOnRebalance = config.LoadTimeFrameToCheckOnRebalance;
        CanUseExcessEnergyState = config.SmartGridReady.CanUseExcessEnergyState;
        EnergyNeededState = config.SmartGridReady.EnergyNeededState;
        CriticalEnergyNeededState = config.SmartGridReady.CriticalEnergyNeededState;
        BlockedTimeWindows = config.SmartGridReady.BlockedTimeWindows.Select(x => new TimeWindow(x.ActiveSensor, x.ActiveSensorInverted, x.Days, x.Start, x.End)).ToList();
        ConfigureStateWatcher();
    }

    private async void BalancingMethodChanged(object? sender, BalancingMethodChangedEventArgs e)
    {
        try
        {
            State.BalancingMethod = e.BalancingMethod;
            Context.Logger.LogInformation("{Name}: Set balancing method to {BalancingMethod}.", Name, e.BalancingMethod);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle BalancingMethodChanged event.");
        }
    }

    private async void BalanceOnBehalfOfChanged(object? sender, BalanceOnBehalfOfChangedEventArgs e)
    {
        try
        {
            State.BalanceOnBehalfOf = e.BalanceOnBehalfOf;
            Context.Logger.LogInformation("{Name}: Set balance on behalf of to {BalanceOnBehalfOf}.", Name, e.BalanceOnBehalfOf);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle BalanceOnBehalfOfChanged event.");
        }
    }

    private async void AllowBatteryPowerChanged(object? sender, AllowBatteryPowerChangedEventArgs e)
    {
        try
        {
            State.AllowBatteryPower = e.AllowBatteryPower;
            Context.Logger.LogInformation("{Name}: Set allow battery power to {AllowBatteryPower}.", Name, e.AllowBatteryPower);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle AllowBatteryPowerChanged event.");
        }
    }

    internal void ConfigureStateWatcher()
    {
        StateWatcherTask = Context.Scheduler.RunEvery(TimeSpan.FromSeconds(5), Context.Scheduler.Now.AddSeconds(3), StateWatcher);
    }

    private async void StateWatcher()
    {
        try
        {
            var changed = BlockedStateMonitor();
            changed |= StateMonitor();

            if (changed)
                await DebounceSaveAndPublishState();
        }
        catch (Exception e)
        {
            Context.Logger.LogWarning(e, "Could check state of heat pump.");
        }
    }

    private bool BlockedStateMonitor()
    {
        var changed = false;
        var isInBlockedTimeWindow = BlockedTimeWindows.Any(timeWindow => timeWindow.IsActive(Context.Scheduler.Now, Context.Timezone));

        if (isInBlockedTimeWindow && SmartGridReadyMode != SmartGridReadyMode.Blocked && (LastSmartGridReadyChangedAt == null || LastSmartGridReadyChangedAt?.AddMinutes(30) < Context.Scheduler.Now))
        {
            Context.Logger.LogInformation("{Name}: Blocked time window - Set smart grid ready mode to blocked.", Name);
            Block();
            changed = true;
        }
        //Unblock happens in rebalance after 15 minutes or when load is below peak load
        return changed;
    }

    private void Block()
    {
        HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Blocked.ToString());
    }

    private void Unblock()
    {
        HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Normal.ToString());
    }

    private void Boost()
    {
        HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Boosted.ToString());
    }

    private void DeBoost()
    {
        HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Normal.ToString());
        EndedBoostAt = Context.Scheduler.Now;
    }

    private bool StateMonitor()
    {
        //Context.Logger.LogInformation("State monitor IsRunning = {IsRunning}. Consumer state = {ConsumerState}. SmartGridReadyMode = {SmartGridReadyMode}", IsRunning.ToString(), State.State.ToString(), SmartGridReadyMode.ToString());

        var changed = false;
        if (State.State == EnergyConsumerState.Running && !IsRunning)
        {
            Stopped();
            changed = true;
        }

        if (State.State != EnergyConsumerState.Running && IsRunning)
        {
            Started();
            changed = true;
        }
        return changed;
    }
    private void SmartGridModeSelect_Changed(object? sender, Entities.Select.SelectEntityEventArgs e)
    {
        LastSmartGridReadyChangedAt = Context.Scheduler.Now;
    }

    private async void StateSensor_StateChanged(object? sender, TextSensorEventArgs e)
    {
        try
        {
            if (!IsRunning)
            {
                if (e.Sensor.State == EnergyNeededState || e.Sensor.State == CanUseExcessEnergyState)
                    State.State = EnergyConsumerState.NeedsEnergy;
                else if (e.Sensor.State == CriticalEnergyNeededState)
                    State.State = EnergyConsumerState.CriticallyNeedsEnergy;
            }
            else
            {
                if (e.Sensor.State != CanUseExcessEnergyState && e.Sensor.State != EnergyNeededState && e.Sensor.State != CriticalEnergyNeededState)
                    Stop();
            }

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "An error occurred while handling change of state sensor.");
        }
    }

    public (double current, double netPowerChange) Rebalance(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double expectedLoadCorrections, double dynamicLoadAdjustments, double dynamicLoadThatCanBeScaledDownOnBehalfOf, double maximumDischargePower)
    {
        var uncorrectedLoad = LoadTimeFrameToCheckOnRebalance switch
        {
            LoadTimeFrames.Now => gridMonitor.CurrentLoadMinusBatteries,
            LoadTimeFrames.SolarForecastNow => gridMonitor.CurrentLoadMinusBatteriesSolarCorrected,
            LoadTimeFrames.SolarForecastNow50PercentCorrected => gridMonitor.CurrentLoadMinusBatteriesSolarCorrected50Percent,
            LoadTimeFrames.SolarForecast30Minutes => gridMonitor.CurrentLoadMinusBatteriesSolarForecast30MinutesCorrected,
            LoadTimeFrames.SolarForecast1Hour => gridMonitor.CurrentLoadMinusBatteriesSolarForecast1HourCorrected,
            LoadTimeFrames.Last30Seconds => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromSeconds(30)),
            LoadTimeFrames.LastMinute => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(1)),
            LoadTimeFrames.Last2Minutes => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(2)),
            LoadTimeFrames.Last5Minutes => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(5)),
            _ => throw new ArgumentOutOfRangeException()
        };
        var consumerAverageLoadCorrection = consumerAverageLoadCorrections[LoadTimeFrameToCheckOnRebalance];
        var estimatedLoad = uncorrectedLoad + consumerAverageLoadCorrection + dynamicLoadAdjustments - dynamicLoadThatCanBeScaledDownOnBehalfOf;

        if (AllowBatteryPower == AllowBatteryPower.MaxPower)
            estimatedLoad -= maximumDischargePower;

        var shouldDeBoost = BalancingMethod switch
        {
            BalancingMethod.SolarSurplus => estimatedLoad > 0,
            BalancingMethod.NearPeak => estimatedLoad > gridMonitor.PeakLoad,
            BalancingMethod.MaximizeQuarterPeak => estimatedLoad > gridMonitor.PeakLoad,
            _ => estimatedLoad > SwitchOffLoad
        };

        //Scale down when needed
        if (shouldDeBoost && SmartGridReadyMode == SmartGridReadyMode.Boosted && (LastSmartGridReadyChangedAt == null || LastSmartGridReadyChangedAt?.AddMinutes(30) < Context.Scheduler.Now))
        {
            Context.Logger.LogInformation("{Name}: Rebalance - Boosted => Normal - Consuming too much energy.", Name);
            DeBoost();
            return (0, 0);
        }

        //Scale down when needed
        if (estimatedLoad > gridMonitor.PeakLoad && SmartGridReadyMode == SmartGridReadyMode.Normal && (EndedBoostAt == null || EndedBoostAt?.AddMinutes(3) < Context.Scheduler.Now))
        {
            Context.Logger.LogInformation("{Name}: Rebalance - Normal => Blocked - Exceeding peak load.", Name);
            Block();
            return (0, 0);
        }

        var isInBlockedTimeWindow = BlockedTimeWindows.Any(timeWindow => timeWindow.IsActive(Context.Scheduler.Now, Context.Timezone));
        if (isInBlockedTimeWindow)
            return (0, 0);

        if (LastSmartGridReadyChangedAt == null || LastSmartGridReadyChangedAt?.AddMinutes(15) >= Context.Scheduler.Now)
            return (0, 0);

        //Keep expected load corrections in mind before boosting
        estimatedLoad += expectedLoadCorrections;
        var canBoost = State.State == EnergyConsumerState.CriticallyNeedsEnergy
            ? estimatedLoad + PeakLoad < gridMonitor.PeakLoad //Will not turn on a load that would exceed current grid import peak
            : estimatedLoad < SwitchOnLoad;


        //Scale up when possible
        if (SmartGridReadyMode == SmartGridReadyMode.Blocked && canBoost)
        {
            Context.Logger.LogInformation("{Name}: Rebalance - Blocked => Normal - Because energy is available.", Name);
            Unblock();
            return (0, 0);
        }

        if (!IsRunning)
            return (0, 0);

        //Scale up when possible
        if (SmartGridReadyMode == SmartGridReadyMode.Normal && canBoost)
        {
            Context.Logger.LogInformation("{Name}: Rebalance - Normal => Boosted - Because energy is available and heat pump is already active.", Name);
            Boost();
        }
        return (0, 0);
    }

    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {
        if (IsRunning && HomeAssistant.StateSensor.State != CanUseExcessEnergyState && HomeAssistant.StateSensor.State != EnergyNeededState && HomeAssistant.StateSensor.State != CriticalEnergyNeededState)
        {
            Stop();
        }

        StateMonitor();
    }

    protected override EnergyConsumerState GetState()
    {
        return IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < Context.Scheduler.Now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.StateSensor.State == CriticalEnergyNeededState => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.StateSensor.State == EnergyNeededState => EnergyConsumerState.NeedsEnergy,
            false when HomeAssistant.StateSensor.State == CanUseExcessEnergyState => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }
    protected override bool CanStart()
    {
        if (SmartGridReadyMode == SmartGridReadyMode.Blocked)
            return false;

        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow() && HasTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(State.LastRun?.Add(MinimumTimeout.Value) > Context.Scheduler.Now);
    }

    public override bool CanForceStop()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (HomeAssistant.StateSensor.State == CriticalEnergyNeededState)
            return false;

        if (EndedBoostAt?.AddMinutes(3) > Context.Scheduler.Now)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (EndedBoostAt?.AddMinutes(3) > Context.Scheduler.Now)
            return false;

        return true;
    }

    public override void TurnOn()
    {
        Context.Logger.LogInformation("{Name}: Set smart grid ready mode to boosted.", Name);
        Boost();
    }

    public override void TurnOff()
    {
        if (SmartGridReadyMode == SmartGridReadyMode.Boosted)
        {
            Context.Logger.LogInformation("{Name}: Turn off - Stopped boost mode. Set smart grid ready mode to normal.", Name);
            DeBoost();
            return;
        }

        if (EndedBoostAt?.AddMinutes(3) > Context.Scheduler.Now)
            return;

        if (SmartGridReadyMode == SmartGridReadyMode.Normal)
        {
            Context.Logger.LogInformation("{Name}: Turn off - Set smart grid ready mode to blocked.", Name);
            Block();
        }
    }

    public override void Dispose()
    {
        HomeAssistant.StateSensor.StateChanged -= StateSensor_StateChanged;
        HomeAssistant.Dispose();
        MqttSensors.Dispose();
        ConsumptionMonitorTask?.Dispose();
        StateWatcherTask?.Dispose();
    }
}