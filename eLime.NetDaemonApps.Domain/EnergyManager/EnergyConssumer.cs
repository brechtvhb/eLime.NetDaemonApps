using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public abstract class EnergyConsumer : IDisposable
{
    public String Name { get; set; }
    public NumericEntity PowerUsage { get; set; }
    public BinarySensor CriticallyNeeded { get; set; }
    public abstract Boolean Running { get; }
    public Double CurrentLoad => PowerUsage.State ?? 0;

    public abstract Double PeakLoad { get; }
    public Double SwitchOnLoad { get; set; }
    public TimeSpan? MinimumRuntime { get; }
    public TimeSpan? MaximumRuntime { get; }
    public TimeSpan? MinimumTimeout { get; }
    public TimeSpan? MaximumTimeout { get; }

    public List<TimeWindow> TimeWindows { get; }
    public DateTimeOffset? StartedAt { get; protected set; }
    public DateTimeOffset? LastRun { get; protected set; }
    public EnergyConsumerState State { get; private set; }

    public IDisposable? StopTimer { get; set; }

    public event EventHandler<EnergyConsumerStateChangedEvent>? StateChanged;

    protected void OnStateCHanged(EnergyConsumerStateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }

    public void SetState(EnergyConsumerState state)
    {
        State = state;
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
        CheckDesiredState(now);
    }


    public void CheckDesiredState(EnergyConsumerStateChangedEvent eventToEmit)
    {
        if (State != eventToEmit.State)
            State = eventToEmit.State;

        eventToEmit.State = State;
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

        var timeUntilEndOfWindow = now - end;

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

        foreach (var timeWindow in TimeWindows)
        {

            var start = now.Add(-now.TimeOfDay).Add(timeWindow.End.ToTimeSpan());
            var end = now.Add(-now.TimeOfDay).Add(timeWindow.End.ToTimeSpan());

            if (timeWindow.Start > timeWindow.End)
            {
                if (now.TimeOfDay > timeWindow.Start.ToTimeSpan())
                    end = end.AddDays(1);

                if (now.TimeOfDay < timeWindow.End.ToTimeSpan())
                    start = start.AddDays(1);
            }

            if (start <= now && end >= now)
                return timeWindow;

        }

        return null;
    }

    internal void Started(ILogger logger, IScheduler scheduler, DateTimeOffset? startTime = null)
    {
        StartedAt = startTime ?? scheduler.Now;
        var timespan = GetRunTime(scheduler.Now);

        switch (timespan)
        {
            case null:
                break;
            case not null when timespan <= TimeSpan.Zero:
                logger.LogDebug("{EnergyConsumer}: Will stop right now.", Name);
                TurnOff();
                break;
            case not null when timespan > TimeSpan.Zero:
                logger.LogDebug("{EnergyConsumer}: Will stop in '{TimeSpan}'", Name, timespan.Round().ToString());
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

    public abstract void TurnOn();
    public abstract void TurnOff();

    public void Dispose()
    {
    }
}

public class TimeWindow
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
}

public enum EnergyConsumerState
{
    Unknown,
    Off,
    Running,
    NeedsEnergy,
    CriticallyNeedsEnergy
}