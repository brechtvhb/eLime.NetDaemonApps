using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Cooling;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Simple;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.SmartGridReady;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;
using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using System.Reactive.Concurrency;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public abstract class EnergyConsumer : IDisposable
{
    protected EnergyManagerContext Context { get; }

    internal ConsumerState State { get; private set; }
    internal abstract EnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal abstract EnergyConsumerMqttSensors MqttSensors { get; }
    internal abstract bool IsRunning { get; }
    internal abstract double PeakLoad { get; }
    public string Name { get; }

    internal List<TimeWindow> TimeWindows { get; init; }
    internal TimeSpan? MinimumRuntime { get; init; }
    internal TimeSpan? MaximumRuntime { get; init; }
    internal TimeSpan? MinimumTimeout { get; init; }
    internal TimeSpan? MaximumTimeout { get; init; }

    protected readonly double _switchOnLoad;
    internal virtual double SwitchOnLoad => _switchOnLoad;

    protected readonly double _switchOffLoad;
    internal virtual double SwitchOffLoad => _switchOffLoad;
    internal List<LoadTimeFrames> LoadTimeFramesToCheckOnStart { get; init; }
    internal List<LoadTimeFrames> LoadTimeFramesToCheckOnStop { get; init; }

    internal List<string> ConsumerGroups { get; init; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }
    public IDisposable? StopTimer { get; set; }

    protected IDisposable? ConsumptionMonitorTask { get; set; }
    private double _lastKnownValidPowerConsumptionValue;
    internal double CurrentLoad
    {
        get
        {
            if (HomeAssistant.PowerConsumptionSensor.State != null)
                _lastKnownValidPowerConsumptionValue = HomeAssistant.PowerConsumptionSensor.State.Value;

            return _lastKnownValidPowerConsumptionValue;
        }
    }

    //Might need to do something on start so we have zero values?
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _recentPowerConsumptionValues = new(200); // With updates every 5 seconds we have at least 15 minutes of data
    internal double AverageLoad(TimeSpan timeSpan)
    {
        var values = _recentPowerConsumptionValues.Where(x => x.Moment.Add(timeSpan) > Context.Scheduler.Now).Select(x => x.Value).ToList();
        return values.Count == 0 ? CurrentLoad : Math.Round(values.Average());
    }

    internal double AverageLoadCorrection(TimeSpan timeSpan)
    {
        var averageLoad = AverageLoad(timeSpan);
        return CurrentLoad - averageLoad;
    }

    protected EnergyConsumer(EnergyManagerContext context, EnergyConsumerConfiguration config)
    {
        Context = context;
        Name = config.Name;
        TimeWindows = config.TimeWindows.Select(x => new TimeWindow(x.ActiveSensor, x.Days, x.Start, x.End)).ToList();

        MinimumRuntime = config.MinimumRuntime;
        MaximumRuntime = config.MaximumRuntime;
        MinimumTimeout = config.MinimumTimeout;
        MaximumTimeout = config.MaximumTimeout;
        ConsumerGroups = config.ConsumerGroups;
        _switchOnLoad = config.SwitchOnLoad;
        _switchOffLoad = config.SwitchOffLoad;
        LoadTimeFramesToCheckOnStart = config.LoadTimeFramesToCheckOnStart;
        LoadTimeFramesToCheckOnStop = config.LoadTimeFramesToCheckOnStop;
    }

    internal void ConfigurePowerConsumptionTask()
    {
        ConsumptionMonitorTask = Context.Scheduler.RunEvery(TimeSpan.FromSeconds(5), Context.Scheduler.Now, GetPowerConsumption);
    }
    private void GetPowerConsumption()
    {
        //Update every 5 seconds instead of listening to HomeAssistant.PowerConsumptionSensor.Changed, it would be more difficult to calculate real averages over a certain timeframe
        if (HomeAssistant.PowerConsumptionSensor.State != null)
            _recentPowerConsumptionValues.Enqueue((Context.Scheduler.Now, HomeAssistant.PowerConsumptionSensor.State.Value));
    }

    public static async Task<EnergyConsumer> Create(EnergyManagerContext context, List<String> allConsumerGroups, EnergyConsumerConfiguration config)
    {
        EnergyConsumer consumer;

        if (config.Simple != null)
            consumer = new SimpleEnergyConsumer(context, config);
        else if (config.Cooling != null)
            consumer = new CoolingEnergyConsumer(context, config);
        else if (config.Triggered != null)
            consumer = new TriggeredEnergyConsumer(context, config);
        else if (config.CarCharger != null)
            consumer = new CarChargerEnergyConsumer(context, config);
        else if (config.SmartGridReady != null)
            consumer = new SmartGridReadyEnergyConsumer(context, config);
        else
            throw new NotSupportedException($"The energy consumer type '{config.GetType().Name}' is not supported.");

        if (context.DebounceDuration != TimeSpan.Zero)
            consumer.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(context.DebounceDuration);

        await consumer.MqttSensors.CreateOrUpdateEntities(allConsumerGroups);
        consumer.GetAndSanitizeState();
        consumer.StopIfPastRuntime();
        consumer.StopOnBootIfEnergyIsNoLongerNeeded();
        consumer.ConfigurePowerConsumptionTask();
        await consumer.SaveAndPublishState();

        return consumer;
    }

    protected abstract void StopOnBootIfEnergyIsNoLongerNeeded();
    internal void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<ConsumerState>("EnergyManager", Name.MakeHaFriendly());
        State = persistedState ?? new ConsumerState();
        if (State.State == EnergyConsumerState.Unknown)
            State.State = GetState();

        Context.Logger.LogDebug("{Name}: Retrieved state ({State})", Name, State.State);
    }

    internal void StopIfPastRuntime()
    {
        var timespan = GetRunTime();

        switch (timespan)
        {
            case null:
                break;
            case not null when timespan <= TimeSpan.Zero:
                Context.Logger.LogDebug("{EnergyConsumer}: Will stop right now.", Name);
                TurnOff();
                break;
            case not null when timespan > TimeSpan.Zero:
                Context.Logger.LogDebug("{EnergyConsumer}: Will run for maximum span of '{TimeSpan}'", Name, timespan.Round().ToString());
                StopTimer = Context.Scheduler.Schedule(timespan.Value, TurnOff);
                break;
        }
    }

    protected async Task DebounceSaveAndPublishState()
    {
        if (SaveAndPublishStateDebounceDispatcher == null)
        {
            await SaveAndPublishState();
            return;
        }

        await SaveAndPublishStateDebounceDispatcher.DebounceAsync(SaveAndPublishState);
    }

    internal async Task SaveAndPublishState()
    {
        Context.FileStorage.Save("EnergyManager", Name.MakeHaFriendly(), State);
        await MqttSensors.PublishState(State);
    }

    public void UpdateState()
    {
        State.State = GetState();
    }

    internal void Started()
    {
        State.StartedAt = Context.Scheduler.Now;
        State.State = EnergyConsumerState.Running;
        Context.Logger.LogDebug("{EnergyConsumer}: Was started.", Name);

        if (MaximumRuntime != null)
        {
            var runTime = GetRunTime();
            if (runTime != null)
                StopTimer = Context.Scheduler.Schedule(runTime.Value, TurnOff);
        }
    }

    internal void Stop()
    {
        TurnOff();
        StopTimer?.Dispose();
        StopTimer = null;
    }

    internal void Stopped()
    {
        State.StartedAt = null;
        State.LastRun = Context.Scheduler.Now;
        State.State = GetState();
        Context.Logger.LogDebug("{EnergyConsumer}: Was stopped.", Name);
    }

    public TimeSpan? GetRunTime()
    {
        if (State.State != EnergyConsumerState.Running)
            return null;

        var timeWindow = GetCurrentTimeWindow();

        if (timeWindow == null)
            return GetRemainingRunTime(MaximumRuntime);

        var end = Context.Scheduler.Now.Add(-Context.Scheduler.Now.TimeOfDay).Add(timeWindow.End.ToTimeSpan());
        if (Context.Scheduler.Now > end)
            end = end.AddDays(1);

        var timeUntilEndOfWindow = end - Context.Scheduler.Now;

        if (MaximumRuntime == null)
            return GetRemainingRunTime(timeUntilEndOfWindow);

        return MaximumRuntime < timeUntilEndOfWindow
            ? GetRemainingRunTime(MaximumRuntime)
            : GetRemainingRunTime(timeUntilEndOfWindow);
    }

    protected bool HasTimeWindow()
    {
        return TimeWindows.Count > 0;
    }
    protected bool IsWithinTimeWindow()
    {
        return GetCurrentTimeWindow() != null;
    }

    protected TimeWindow? GetCurrentTimeWindow()
    {
        if (!HasTimeWindow())
            return null;

        //if (Name == "Washing machine")
        //    Logger.LogDebug($"TimeWindows: {String.Join(" / ", TimeWindows.Select(x => x.ToString()))}");

        return TimeWindows.FirstOrDefault(timeWindow => timeWindow.IsActive(Context.Scheduler.Now, Context.Timezone));
    }


    protected TimeSpan? GetRemainingRunTime(TimeSpan? suggestedRunTime)
    {
        var currentRuntime = Context.Scheduler.Now - State.StartedAt;
        var remainingDuration = suggestedRunTime - currentRuntime;
        return remainingDuration < suggestedRunTime ? remainingDuration : suggestedRunTime;
    }
    protected abstract EnergyConsumerState GetState();
    protected abstract bool CanStart();

    public virtual bool CanStart(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double expectedLoadCorrections, double dynamicLoadAdjustments, double dynamicLoadThatCanBeScaledDownOnBehalfOf, double startLoadAdjustments)
    {
        var canStart = CanStart();

        if (!canStart)
            return false;

        canStart = true;

        foreach (var timeFrameToValidate in LoadTimeFramesToCheckOnStart)
        {
            var uncorrectedLoad = timeFrameToValidate switch
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

            var consumerAverageLoadCorrection = consumerAverageLoadCorrections[timeFrameToValidate];

            var estimatedLoad = uncorrectedLoad + consumerAverageLoadCorrection + dynamicLoadAdjustments + startLoadAdjustments - dynamicLoadThatCanBeScaledDownOnBehalfOf;
            if (this is not IDynamicLoadConsumer) //Ignore expectedLoadCorrections for dynamic consumers
                estimatedLoad += expectedLoadCorrections;

            var allowed = State.State == EnergyConsumerState.CriticallyNeedsEnergy
                ? estimatedLoad + PeakLoad < gridMonitor.PeakLoad //Will not turn on a load that would exceed current grid import peak
                : estimatedLoad < SwitchOnLoad;

            canStart &= allowed;
        }

        return canStart;
    }

    public abstract bool CanForceStop();
    public abstract bool CanForceStopOnPeakLoad();

    //At the moment we do nothing with expectedLoadCorrections when stopping
    public virtual bool CanStop(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double expectedLoadCorrections, double dynamicLoadAdjustments, double dynamicLoadThatCanBeScaledDownOnBehalfOf, double startLoadAdjustments, double stopLoadAdjustments)
    {
        var canForceStop = CanForceStop();
        var canForceStopOnPeakLoad = CanForceStopOnPeakLoad();

        if (!canForceStop && canForceStopOnPeakLoad)
            return false;

        if (canForceStopOnPeakLoad && this is IDynamicLoadConsumer && dynamicLoadAdjustments != 0)
            return false;

        var canStop = true;
        foreach (var timeFrameToValidate in LoadTimeFramesToCheckOnStop)
        {
            var uncorrectedLoad = timeFrameToValidate switch
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
            var consumerAverageLoadCorrection = consumerAverageLoadCorrections[timeFrameToValidate];
            var estimatedLoad = uncorrectedLoad + consumerAverageLoadCorrection + dynamicLoadAdjustments + startLoadAdjustments + stopLoadAdjustments - dynamicLoadThatCanBeScaledDownOnBehalfOf;

            var allowed = false;

            if (canForceStop)
                allowed = estimatedLoad > SwitchOffLoad;
            if (!allowed && canForceStopOnPeakLoad)
                allowed = estimatedLoad >= gridMonitor.PeakLoad;

            canStop &= allowed;
        }

        return canStop;
    }
    public abstract void TurnOn();
    public abstract void TurnOff();
    public abstract void Dispose();
}