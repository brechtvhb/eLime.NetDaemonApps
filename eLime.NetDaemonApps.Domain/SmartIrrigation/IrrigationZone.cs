using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;
#pragma warning disable CS8618

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public abstract class IrrigationZone : IDisposable
{
    public ILogger Logger { get; private set; }
    public String Name { get; private set; }
    public Int32 FlowRate { get; private set; }
    public BinarySwitch Valve { get; private set; }

    public Boolean CurrentlyWatering => Valve.IsOn();
    public DateTimeOffset? WateringStartedAt { get; protected set; }
    public DateTimeOffset? LastWatering { get; protected set; }

    public NeedsWatering State { get; private set; }

    public ZoneMode Mode { get; private set; }

    public DateTimeOffset? LastNotification { get; private set; }

    public IDisposable? ModeChangedCommandHandler { get; set; }
    public IDisposable? StopTimer { get; set; }

    public IScheduler Scheduler { get; private set; }
    public DateTimeOffset? IrrigationSeasonStart { get; private set; }
    public DateTimeOffset? IrrigationSeasonEnd { get; private set; }


    internal IrrigationZoneFileStorage ToFileStorage() => new()
    {
        State = State,
        Mode = Mode,
        StartedAt = WateringStartedAt,
        LastRun = LastWatering,
    };


    protected void SetCommonFields(ILogger logger, String name, Int32 flowRate, BinarySwitch valve, IScheduler scheduler, DateTimeOffset? irrigationSeasonStart, DateTimeOffset? irrigationSeasonEnd)
    {
        Logger = logger;

        Name = name;
        FlowRate = flowRate;
        Valve = valve;
        Valve.TurnedOn += Valve_TurnedOn;
        Valve.TurnedOff += Valve_TurnedOff;

        Scheduler = scheduler;
        IrrigationSeasonStart = irrigationSeasonStart;
        IrrigationSeasonEnd = irrigationSeasonEnd;
    }

    protected void AdjustYearIfNeeded()
    {
        IrrigationSeasonStart = AdjustYearIfNeeded(IrrigationSeasonStart);
        IrrigationSeasonEnd = AdjustYearIfNeeded(IrrigationSeasonEnd);
    }

    protected DateTimeOffset? AdjustYearIfNeeded(DateTimeOffset? date)
    {
        if (date == null)
            return null;

        if (date.Value.Year == Scheduler.Now.Year)
            return date;

        return new DateTimeOffset(Scheduler.Now.Year, date.Value.Month, date.Value.Day, 0, 0, 0, date.Value.Offset);
    }

    protected bool WithinIrrigationSeason => !(IrrigationSeasonStart > Scheduler.Now) && !(IrrigationSeasonEnd < Scheduler.Now);
    protected bool HasIrrigationSeason => IrrigationSeasonStart != null || IrrigationSeasonEnd != null;

    private void Valve_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new IrrigationZoneWateringStartedEvent(this, State));
    }

    private void Valve_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new IrrigationZoneWateringEndedEvent(this, State));
    }

    public void SetState(NeedsWatering state, DateTimeOffset? startedAt, DateTimeOffset? lastRun)
    {
        State = state;

        if (startedAt != null && State == NeedsWatering.Ongoing)
            WateringStartedAt = startedAt;

        if (lastRun != null)
            LastWatering = lastRun;

        CheckDesiredState();
    }

    public void NotificationSent(DateTimeOffset now)
    {
        LastNotification = now;
    }


    public void SetMode(ZoneMode mode)
    {
        Mode = mode;
    }

    public void Start()
    {
        Valve.TurnOn();
    }

    public void Started(ILogger logger, IScheduler scheduler, DateTimeOffset? startTime = null)
    {
        WateringStartedAt = startTime;


        if (WateringStartedAt == null)
            return;

        if (this is not IZoneWithLimitedRuntime limitedRunTimeZone)
            return;

        //Disable automatic turning off of watering for this zone type when mode is Manual. For other zones automatic turn off is enabled when mode is off because I forget tend to forget that I turned it on manually.
        if (this is AntiFrostMistingIrrigationZone && Mode == ZoneMode.Manual)
            return;

        var timespan = limitedRunTimeZone.GetRunTime(logger, scheduler.Now);

        switch (timespan)
        {
            case null:
                return;
            case not null when timespan <= TimeSpan.Zero:
                logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone right now.", Name);
                Stop();
                return;
            case not null when timespan > TimeSpan.Zero:
                logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone in '{TimeSpan}'", Name, timespan.Round().ToString());
                StopTimer = scheduler.Schedule(timespan.Value, Stop);
                return;
        }

        CheckDesiredState();
    }

    public void Stop()
    {
        Valve.TurnOff();
        StopTimer?.Dispose();
        StopTimer = null;


    }

    public void SetLastWateringDate(DateTimeOffset now)
    {
        WateringStartedAt = null;
        LastWatering = now;
        CheckDesiredState();
    }

    public event EventHandler<IrrigationZoneStateChangedEvent>? StateChanged;

    protected void OnStateCHanged(IrrigationZoneStateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }


    public void CheckDesiredState(IrrigationZoneStateChangedEvent? eventToEmit = null)
    {
        var desiredState = GetDesiredState();

        if (State == desiredState)
            return;

        State = desiredState;

        if (eventToEmit != null)
        {
            eventToEmit.State = State;
            OnStateCHanged(eventToEmit);
            return;
        }

        IrrigationZoneStateChangedEvent? @event = desiredState switch
        {
            NeedsWatering.Yes => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.Critical => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.No => new IrrigationZoneEndWateringEvent(this, State),
            NeedsWatering.Ongoing => null,
            NeedsWatering.Unknown => null,
            _ => null
        };

        if (@event != null)
            OnStateCHanged(@event);
    }

    protected abstract NeedsWatering GetDesiredState();
    public abstract bool CanStartWatering(DateTimeOffset now, bool energyAvailable);
    public abstract bool CheckForForceStop(DateTimeOffset now);

    protected TimeSpan? GetRemainingRunTime(TimeSpan? suggestedRunTime, DateTimeOffset now)
    {
        var currentRuntime = now - WateringStartedAt;
        var remainingDuration = suggestedRunTime - currentRuntime;
        return remainingDuration < suggestedRunTime ? remainingDuration : suggestedRunTime;
    }

    public void Dispose()
    {
        ModeChangedCommandHandler?.Dispose();
        StopTimer?.Dispose();

        Valve.TurnedOn -= Valve_TurnedOn;
        Valve.TurnedOn -= Valve_TurnedOff;
        Valve.Dispose();
    }
}