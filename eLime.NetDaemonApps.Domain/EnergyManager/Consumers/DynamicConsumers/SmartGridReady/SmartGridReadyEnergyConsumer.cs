using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.SmartHeatPump;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.SmartGridReady;

public class SmartGridReadyEnergyConsumer : EnergyConsumer, IDynamicLoadConsumer
{
    private static readonly TimeSpan StateChangeCooldown = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PeakLoadScaleDownCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeBoostCooldown = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DeBlockCooldown = TimeSpan.FromMinutes(15);
    private static readonly double RunningPowerThreshold = 100;

    public BalancingMethod BalancingMethod => State.BalancingMethod ?? BalancingMethod.SolarOnly;
    public string BalanceOnBehalfOf => State.BalanceOnBehalfOf ?? IDynamicLoadConsumer.CONSUMER_GROUP_SELF;
    public AllowBatteryPower AllowBatteryPower => State.AllowBatteryPower ?? AllowBatteryPower.No;
    public double ReleasablePowerWhenBalancingOnBehalfOf => HomeAssistant.StateSensor.State == CanUseExcessEnergyState
        ? HomeAssistant.PowerConsumptionSensor.State ?? 0
        : 0;
    public bool ApplyExpectedLoadCorrections => true;

    internal List<DynamicEnergyConsumerBalancingMethodBasedLoads> DynamicBalancingMethodBasedLoads { get; }

    private DynamicEnergyConsumerBalancingMethodBasedLoads? CurrentBalancingMethodConfig => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))
        : null;

    internal override double SwitchOnLoad => CurrentBalancingMethodConfig?.SwitchOnLoad ?? _switchOnLoad;
    internal override double SwitchOffLoad => CurrentBalancingMethodConfig?.SwitchOffLoad ?? _switchOffLoad;

    private LoadTimeFrames _loadTimeFrameToCheckOnRebalance { get; }
    internal LoadTimeFrames LoadTimeFrameToCheckOnRebalance => CurrentBalancingMethodConfig?.LoadTimeFrameToCheckOnRebalance ?? _loadTimeFrameToCheckOnRebalance;

    internal sealed override DynamicEnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override SmartGridReadyEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

    internal override bool IsRunning => HomeAssistant.PowerConsumptionSensor.State > RunningPowerThreshold || HomeAssistant.GetSmartGridReadyMode() == SmartGridReadyMode.Boosted;

    private readonly double _fallbackPeakLoad;
    internal override double PeakLoad => HomeAssistant.ExpectedPeakLoadSensor.State is > 0
        ? HomeAssistant.ExpectedPeakLoadSensor.State.Value
        : _fallbackPeakLoad;

    internal string CanUseExcessEnergyState { get; }
    internal string EnergyNeededState { get; }
    internal string CriticalEnergyNeededState { get; }
    internal List<TimeWindow> BlockedTimeWindows { get; set; }
    protected IDisposable? StateWatcherTask { get; set; }
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
            var smartGridMode = HomeAssistant.GetSmartGridReadyMode();
            var changed = BlockedStateMonitor(smartGridMode) | StateMonitor(smartGridMode);

            if (changed)
                await DebounceSaveAndPublishState();
        }
        catch (Exception e)
        {
            Context.Logger.LogWarning(e, "Could check state of heat pump.");
        }
    }

    private bool BlockedStateMonitor(SmartGridReadyMode currentMode)
    {
        if (!IsInBlockedTimeWindow() || currentMode == SmartGridReadyMode.Blocked || !HasTimePassedSince(LastSmartGridReadyChangedAt, StateChangeCooldown))
            return false;

        Context.Logger.LogInformation("{Name}: Blocked time window - Set smart grid ready mode to blocked.", Name);
        Block();
        return true;

        //Unblock happens in rebalance after 15 minutes or when load is below peak load
    }

    private void Block() => HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Blocked.ToString());

    private void Unblock() => HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Normal.ToString());

    private void Boost() => HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Boosted.ToString());

    private void DeBoost()
    {
        HomeAssistant.SmartGridModeSelect.Change(SmartGridReadyMode.Normal.ToString());
    }

    private bool StateMonitor(SmartGridReadyMode currentMode)
    {
        var isPowerRunning = HomeAssistant.PowerConsumptionSensor.State > RunningPowerThreshold;
        var isBoosted = currentMode == SmartGridReadyMode.Boosted;
        var isActuallyRunning = isPowerRunning || isBoosted;

        if (State.State == EnergyConsumerState.Running && !isActuallyRunning)
        {
            Stopped();
            return true;
        }

        if (State.State != EnergyConsumerState.Running && isActuallyRunning)
        {
            Started();
            return true;
        }

        return false;
    }

    private void SmartGridModeSelect_Changed(object? sender, Entities.Select.SelectEntityEventArgs e)
        => LastSmartGridReadyChangedAt = Context.Scheduler.Now;

    private async void StateSensor_StateChanged(object? sender, TextSensorEventArgs e)
    {
        try
        {
            var sensorState = e.Sensor.State;

            if (!IsRunning)
            {
                State.State = sensorState switch
                {
                    _ when sensorState == CriticalEnergyNeededState => EnergyConsumerState.CriticallyNeedsEnergy,
                    _ when sensorState == EnergyNeededState || sensorState == CanUseExcessEnergyState => EnergyConsumerState.NeedsEnergy,
                    _ => State.State
                };
            }
            else if (!IsInEnergyNeededState(sensorState))
                Stop();

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "An error occurred while handling change of state sensor.");
        }
    }

    public (double current, double netPowerChange) Rebalance(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double expectedLoadCorrections, double dynamicLoadAdjustments, double dynamicLoadThatCanBeScaledDownOnBehalfOf, double maximumDischargePower)
    {
        var smartGridMode = HomeAssistant.GetSmartGridReadyMode();
        var loadTimeFrame = LoadTimeFrameToCheckOnRebalance;
        var uncorrectedLoad = GetLoadForTimeFrame(gridMonitor, loadTimeFrame);
        var consumerAverageLoadCorrection = consumerAverageLoadCorrections[loadTimeFrame];
        var estimatedLoad = uncorrectedLoad + consumerAverageLoadCorrection + dynamicLoadAdjustments - dynamicLoadThatCanBeScaledDownOnBehalfOf;

        if (AllowBatteryPower == AllowBatteryPower.MaxPower)
            estimatedLoad -= maximumDischargePower;

        var isAbovePeakLoad = estimatedLoad > gridMonitor.PeakLoad;

        if (isAbovePeakLoad && HasTimePassedSince(LastSmartGridReadyChangedAt, PeakLoadScaleDownCooldown))
        {
            switch (smartGridMode)
            {
                case SmartGridReadyMode.Boosted:
                    LogModeChange("Rebalance - Boosted => Normal - Exceeding peak load.");
                    DeBoost();
                    return (0, 0);
                case SmartGridReadyMode.Normal:
                    LogModeChange("Rebalance - Normal => Blocked - Exceeding peak load.");
                    Block();
                    return (0, 0);
            }
        }
        var shouldDeBoost = BalancingMethod switch
        {
            BalancingMethod.SolarSurplus => estimatedLoad > 0,
            BalancingMethod.NearPeak or BalancingMethod.MaximizeQuarterPeak => isAbovePeakLoad,
            _ => estimatedLoad > SwitchOffLoad
        };

        //Scale down: Boosted => Normal
        if (shouldDeBoost && smartGridMode == SmartGridReadyMode.Boosted && HasTimePassedSince(LastSmartGridReadyChangedAt, StateChangeCooldown))
        {
            LogModeChange("Rebalance - Boosted => Normal - Consuming too much energy.");
            DeBoost();
            return (0, 0);
        }

        if (IsInBlockedTimeWindow() || !HasTimePassedSince(LastSmartGridReadyChangedAt, DeBlockCooldown))
            return (0, 0);

        //Keep expected load corrections in mind before boosting
        estimatedLoad += expectedLoadCorrections;
        var canBoost = State.State == EnergyConsumerState.CriticallyNeedsEnergy
            ? estimatedLoad + PeakLoad < gridMonitor.PeakLoad //Will not turn on a load that would exceed current grid import peak
            : estimatedLoad < SwitchOnLoad;

        //Scale up: Blocked => Normal
        if (smartGridMode == SmartGridReadyMode.Blocked && canBoost)
        {
            LogModeChange("Rebalance - Blocked => Normal - Energy is available.");
            Unblock();
            return (0, 0);
        }

        if (!IsRunning) //Implicitly waits DeBlockCooldown before boosting from Normal => Boosted as this check happens above
            return (0, 0);

        //Scale up: Normal => Boosted
        if (smartGridMode == SmartGridReadyMode.Normal && canBoost)
        {
            LogModeChange("Rebalance - Normal => Boosted - Energy is available and heat pump is already active (reduce amount of compressor start / stops).");
            Boost();
        }
        return (0, 0);
    }

    private bool IsInBlockedTimeWindow() => BlockedTimeWindows.Any(timeWindow => timeWindow.IsActive(Context.Scheduler.Now, Context.Timezone));

    private bool HasTimePassedSince(DateTimeOffset? timestamp, TimeSpan timeSpan) => timestamp == null || timestamp.Value.Add(timeSpan) < Context.Scheduler.Now;

    private void LogModeChange(string message) => Context.Logger.LogInformation("{Name}: {Message}", Name, message);

    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {
        if (IsRunning && !IsInEnergyNeededState(HomeAssistant.StateSensor.State))
            Stop();

        StateMonitor(HomeAssistant.GetSmartGridReadyMode());
    }

    protected override EnergyConsumerState GetState()
    {
        var sensorState = HomeAssistant.StateSensor.State;

        return IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < Context.Scheduler.Now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when sensorState == CriticalEnergyNeededState => EnergyConsumerState.CriticallyNeedsEnergy,
            false when sensorState == EnergyNeededState || sensorState == CanUseExcessEnergyState => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }

    protected override bool CanStart()
    {
        if (HomeAssistant.GetSmartGridReadyMode() == SmartGridReadyMode.Blocked)
            return false;

        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow() && HasTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return HasTimePassedSince(State.LastRun, MinimumTimeout.Value);
    }

    public override bool CanForceStop()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (HomeAssistant.StateSensor.State == CriticalEnergyNeededState)
            return false;

        return HasTimePassedSince(LastSmartGridReadyChangedAt, DeBoostCooldown);
    }

    public override bool CanForceStopOnPeakLoad()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        return HasTimePassedSince(LastSmartGridReadyChangedAt, DeBoostCooldown);
    }

    public override void TurnOn()
    {
        Context.Logger.LogInformation("{Name}: Set smart grid ready mode to boosted.", Name);
        Boost();
    }

    public override void TurnOff()
    {
        var smartGridMode = HomeAssistant.GetSmartGridReadyMode();

        if (smartGridMode == SmartGridReadyMode.Boosted)
        {
            Context.Logger.LogInformation("{Name}: Turn off - Stopped boost mode. Set smart grid ready mode to normal.", Name);
            DeBoost();
            return;
        }

        if (!HasTimePassedSince(LastSmartGridReadyChangedAt, DeBoostCooldown))
            return;

        if (smartGridMode == SmartGridReadyMode.Normal)
        {
            Context.Logger.LogInformation("{Name}: Turn off - Set smart grid ready mode to blocked.", Name);
            Block();
        }
    }

    public override void Dispose()
    {
        HomeAssistant.StateSensor.StateChanged -= StateSensor_StateChanged;
        HomeAssistant.SmartGridModeSelect.Changed -= SmartGridModeSelect_Changed;
        HomeAssistant.Dispose();
        MqttSensors.BalancingMethodChanged -= BalancingMethodChanged;
        MqttSensors.BalanceOnBehalfOfChanged -= BalanceOnBehalfOfChanged;
        MqttSensors.AllowBatteryPowerChanged -= AllowBatteryPowerChanged;
        MqttSensors.Dispose();
        ConsumptionMonitorTask?.Dispose();
        StateWatcherTask?.Dispose();
    }

    private bool IsInEnergyNeededState(string? state)
        => state == EnergyNeededState || state == CanUseExcessEnergyState || state == CriticalEnergyNeededState;
}