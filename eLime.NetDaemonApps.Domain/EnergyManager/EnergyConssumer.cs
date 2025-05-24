using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public abstract class EnergyConsumer : IDisposable
{
    protected ILogger Logger;

    public String Name { get; private set; }
    public List<String> ConsumerGroups { get; private set; }
    public NumericEntity PowerUsage { get; private set; }
    public BinarySensor? CriticallyNeeded { get; private set; }
    public abstract Boolean Running { get; }
    public Double CurrentLoad => PowerUsage.State ?? 0;

    public abstract Double PeakLoad { get; }
    public Double SwitchOnLoad { get; private set; }
    public Double SwitchOffLoad { get; private set; }

    public TimeSpan? MinimumRuntime { get; private set; }
    public TimeSpan? MaximumRuntime { get; private set; }
    public TimeSpan? MinimumTimeout { get; private set; }
    public TimeSpan? MaximumTimeout { get; private set; }

    public List<TimeWindow> TimeWindows { get; private set; }
    public String Timezone { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? LastRun { get; private set; }
    public EnergyConsumerState State { get; private set; }
    public IDisposable? StopTimer { get; set; }

    public event EventHandler<EnergyConsumerStateChangedEvent>? StateChanged;

    internal EnergyConsumerFileStorage ToFileStorage()
    {
        var fileStorage = new EnergyConsumerFileStorage
        {
            State = State,
            StartedAt = StartedAt,
            LastRun = LastRun,
        };

        if (this is not IDynamicLoadConsumer dynamicLoadConsumer)
            return fileStorage;

        fileStorage.BalancingMethod = dynamicLoadConsumer.BalancingMethod;
        fileStorage.BalanceOnBehalfOf = dynamicLoadConsumer.BalanceOnBehalfOf;
        fileStorage.AllowBatteryPower = dynamicLoadConsumer.AllowBatteryPower;

        return fileStorage;
    }

    protected void SetCommonFields(ILogger logger, String name, List<string> consumerGroups, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout, TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, String timezone)
    {
        Logger = logger;

        Name = name;
        ConsumerGroups = consumerGroups;

        PowerUsage = powerUsage;
        CriticallyNeeded = criticallyNeeded;
        SwitchOffLoad = switchOffLoad;
        SwitchOnLoad = switchOnLoad;

        MinimumRuntime = minimumRuntime;
        MaximumRuntime = maximumRuntime;
        MinimumTimeout = minimumTimeout;
        MaximumTimeout = maximumTimeout;

        TimeWindows = timeWindows;
        Timezone = timezone;
    }

    protected void OnStateCHanged(EnergyConsumerStateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }

    public void SetState(IScheduler scheduler, EnergyConsumerState state, DateTimeOffset? startedAt, DateTimeOffset? lastRun)
    {
        State = state;

        if (startedAt != null && State == EnergyConsumerState.Running)
            Started(scheduler, startedAt);

        if (lastRun != null)
            LastRun = lastRun;
    }


    public void Stop()
    {
        TurnOff();
        StopTimer?.Dispose();
        StopTimer = null;
    }

    public void Stopped(DateTimeOffset now)
    {
        StartedAt = null;
        LastRun = now;

        Logger.LogDebug("{EnergyConsumer}: Was stopped.", Name);

        CheckDesiredState(now);
    }


    public void CheckDesiredState(EnergyConsumerStateChangedEvent eventToEmit)
    {
        State = eventToEmit.State;
        OnStateCHanged(eventToEmit);
    }

    public void CheckDesiredState(DateTimeOffset? now)
    {
        var desiredState = GetDesiredState(now);

        if (State == desiredState)
            return;

        State = desiredState;

        EnergyConsumerStateChangedEvent? @event = desiredState switch
        {
            EnergyConsumerState.NeedsEnergy => new EnergyConsumerStartCommand(this, State),
            EnergyConsumerState.CriticallyNeedsEnergy => new EnergyConsumerStartCommand(this, State),
            EnergyConsumerState.Off => new EnergyConsumerStopCommand(this, State),
            EnergyConsumerState.Running => null,
            EnergyConsumerState.Unknown => null,
            _ => null
        };

        if (@event != null)
            OnStateCHanged(@event);
    }

    public TimeSpan? GetRunTime(DateTimeOffset now)
    {
        var timeWindow = GetCurrentTimeWindow(now);

        if (timeWindow == null)
            return GetRemainingRunTime(MaximumRuntime, now);

        var end = now.Add(-now.TimeOfDay).Add(timeWindow.End.ToTimeSpan());
        if (now > end)
            end = end.AddDays(1);

        var timeUntilEndOfWindow = end - now;

        if (MaximumRuntime == null)
            return GetRemainingRunTime(timeUntilEndOfWindow, now);

        return MaximumRuntime < timeUntilEndOfWindow
            ? GetRemainingRunTime(MaximumRuntime, now)
            : GetRemainingRunTime(timeUntilEndOfWindow, now);
    }

    protected bool HasTimeWindow()
    {
        return TimeWindows.Count > 0;
    }
    protected bool IsWithinTimeWindow(DateTimeOffset now)
    {
        return GetCurrentTimeWindow(now) != null;
    }

    protected TimeWindow? GetCurrentTimeWindow(DateTimeOffset now)
    {
        if (!HasTimeWindow())
            return null;

        //if (Name == "Washing machine")
        //    Logger.LogDebug($"TimeWindows: {String.Join(" / ", TimeWindows.Select(x => x.ToString()))}");

        return TimeWindows.FirstOrDefault(timeWindow => timeWindow.IsActive(now, Timezone));
    }

    internal void Started(IScheduler scheduler, DateTimeOffset? startTime = null)
    {
        StartedAt = startTime ?? scheduler.Now;
        var timespan = GetRunTime(scheduler.Now);

        switch (timespan)
        {
            case null:
                break;
            case not null when timespan <= TimeSpan.Zero:
                Logger.LogDebug("{EnergyConsumer}: Will stop right now.", Name);
                TurnOff();
                break;
            case not null when timespan > TimeSpan.Zero:
                Logger.LogDebug("{EnergyConsumer}: Will run for maximum span of '{TimeSpan}'", Name, timespan.Round().ToString());
                StopTimer = scheduler.Schedule(timespan.Value, TurnOff);
                break;
        }
    }

    protected TimeSpan? GetRemainingRunTime(TimeSpan? suggestedRunTime, DateTimeOffset now)
    {
        var currentRuntime = now - StartedAt;
        var remainingDuration = suggestedRunTime - currentRuntime;
        return remainingDuration < suggestedRunTime ? remainingDuration : suggestedRunTime;
    }

    protected abstract EnergyConsumerState GetDesiredState(DateTimeOffset? now);
    public abstract bool CanStart(DateTimeOffset now);
    public abstract bool CanForceStop(DateTimeOffset now);
    public abstract bool CanForceStopOnPeakLoad(DateTimeOffset now);
    public abstract void TurnOn();
    public abstract void TurnOff();

    public abstract void DisposeInternal();

    public void Dispose()
    {
        CriticallyNeeded?.Dispose();
        StopTimer?.Dispose();
        DisposeInternal();
    }
}

public enum EnergyConsumerState
{
    Unknown,
    Off,
    Running,
    NeedsEnergy,
    CriticallyNeedsEnergy
}