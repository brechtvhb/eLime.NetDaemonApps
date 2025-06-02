using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

public abstract class EnergyConsumer2 : IDisposable
{
    protected EnergyConsumer2(ILogger logger, IFileStorage fileStorage, string timeZone, EnergyConsumerConfiguration config)
    {
        Logger = logger;
        FileStorage = fileStorage;

        TimeWindows = config.TimeWindows.Select(x => new TimeWindow(x.ActiveSensor, new TimeOnly(0, 0).Add(x.Start), new TimeOnly(0, 0).Add(x.End))).ToList(); //TODO clean up
        Timezone = timeZone;

        MinimumRuntime = config.MinimumRuntime;
        MaximumRuntime = config.MaximumRuntime;
        MinimumTimeout = config.MinimumTimeout;
        MaximumTimeout = config.MaximumTimeout;
        ConsumerGroups = config.ConsumerGroups;
        SwitchOnLoad = config.SwitchOnLoad;
        SwitchOffLoad = config.SwitchOffLoad;
    }
    public static async Task<EnergyConsumer2> Create(ILogger logger, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, string timeZone, EnergyConsumerConfiguration config)
    {
        EnergyConsumer2 consumer;

        if (config.Simple != null)
            consumer = new SimpleEnergyConsumer2(logger, fileStorage, mqttEntityManager, timeZone, config);
        else
            throw new NotSupportedException($"The energy consumer type '{config.GetType().Name}' is not supported.");

        await consumer.MqttSensors.CreateOrUpdateEntities(config.ConsumerGroups);
        consumer.GetAndSanitizeState();
        await consumer.SaveAndPublishState();

        return consumer;
    }

    protected ILogger Logger;
    protected IFileStorage FileStorage { get; }

    internal ConsumerState State { get; private set; }
    internal abstract EnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal abstract bool IsRunning { get; }
    internal abstract double PeakLoad { get; }
    internal abstract EnergyConsumerMqttSensors MqttSensors { get; }

    public string Name => State.Name;
    internal double CurrentLoad => HomeAssistant.PowerUsageSensor.State ?? 0;
    internal List<TimeWindow> TimeWindows;
    internal string Timezone;
    internal TimeSpan? MinimumRuntime;
    internal TimeSpan? MaximumRuntime;
    internal TimeSpan? MinimumTimeout;
    internal TimeSpan? MaximumTimeout;
    internal double SwitchOnLoad;
    internal double SwitchOffLoad;
    internal List<string> ConsumerGroups;

    public IDisposable? StopTimer { get; set; }

    internal event EventHandler<EnergyConsumer2StateChangedEvent>? StateChanged;

    internal void GetAndSanitizeState()
    {
        var persistedState = FileStorage.Get<ConsumerState>("EnergyManager", State.Name);
        State = persistedState ?? new ConsumerState();


        //if (HomeAssistant.SourcePumpRunningSensor.IsOn() && State.SourcePumpStartedAt == null)
        //    State.SourcePumpStartedAt = Scheduler.Now;

        //if (HomeAssistant.SourcePumpRunningSensor.IsOff() && State.SourcePumpStartedAt != null)
        //    State.SourcePumpStartedAt = null;

        //State.HeatCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HeatConsumedTodayIntegerSensor.State, HomeAssistant.HeatConsumedTodayDecimalsSensor.State, HomeAssistant.HeatProducedTodayIntegerSensor.State, HomeAssistant.HeatProducedTodayDecimalsSensor.State);
        //State.HotWaterCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HotWaterConsumedTodayIntegerSensor.State, HomeAssistant.HotWaterConsumedTodayDecimalsSensor.State, HomeAssistant.HotWaterProducedTodayIntegerSensor.State, HomeAssistant.HotWaterProducedTodayDecimalsSensor.State);

        Logger.LogDebug("{Name}: Retrieved state", State.Name);
    }

    internal async Task SaveAndPublishState()
    {
        FileStorage.Save("EnergyManager", State.Name, State);
        await MqttSensors.PublishState(State);
    }


    internal void OnStateCHanged(EnergyConsumer2StateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }

    public void SetState(IScheduler scheduler, EnergyConsumerState state, DateTimeOffset? startedAt, DateTimeOffset? lastRun)
    {
        State.State = state;

        if (startedAt != null && state == EnergyConsumerState.Running)
            Started(scheduler, startedAt);

        if (lastRun != null)
            State.LastRun = lastRun;
    }


    internal void Stop()
    {
        TurnOff();
        StopTimer?.Dispose();
        StopTimer = null;
    }

    internal void Stopped(DateTimeOffset now)
    {
        State.StartedAt = null;
        State.LastRun = now;

        Logger.LogDebug("{EnergyConsumer}: Was stopped.", Name);

        CheckDesiredState(now);
    }


    internal void CheckDesiredState(EnergyConsumer2StateChangedEvent eventToEmit)
    {
        State.State = eventToEmit.State;
        OnStateCHanged(eventToEmit);
    }

    public void CheckDesiredState(DateTimeOffset? now)
    {
        var desiredState = GetDesiredState(now);

        if (State.State == desiredState)
            return;

        State.State = desiredState;

        EnergyConsumer2StateChangedEvent? @event = desiredState switch
        {
            EnergyConsumerState.NeedsEnergy => new EnergyConsumer2StartCommand(this, State.State),
            EnergyConsumerState.CriticallyNeedsEnergy => new EnergyConsumer2StartCommand(this, State.State),
            EnergyConsumerState.Off => new EnergyConsumer2StopCommand(this, State.State),
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
        State.StartedAt = startTime ?? scheduler.Now;
        var timespan = GetRunTime(scheduler.Now);

        switch (timespan)
        {
            case null:
                break;
            case not null when timespan <= TimeSpan.Zero:
                Logger.LogDebug("{EnergyConsumer}: Will stop right now.", State.Name);
                TurnOff();
                break;
            case not null when timespan > TimeSpan.Zero:
                Logger.LogDebug("{EnergyConsumer}: Will run for maximum span of '{TimeSpan}'", State.Name, timespan.Round().ToString());
                StopTimer = scheduler.Schedule(timespan.Value, TurnOff);
                break;
        }
    }

    protected TimeSpan? GetRemainingRunTime(TimeSpan? suggestedRunTime, DateTimeOffset now)
    {
        var currentRuntime = now - State.StartedAt;
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
        HomeAssistant.Dispose();
        MqttSensors.Dispose();
        DisposeInternal();
    }
}