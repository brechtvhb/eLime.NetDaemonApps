using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Cooling;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Simple;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public abstract class EnergyConsumer : IDisposable
{
    protected EnergyManagerContext Context { get; }

    internal ConsumerState State { get; private set; }
    internal abstract EnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal abstract bool IsRunning { get; }
    internal abstract double PeakLoad { get; }
    internal abstract EnergyConsumerMqttSensors MqttSensors { get; }
    public string Name { get; private set; }

    internal double CurrentLoad => HomeAssistant.PowerConsumptionSensor.State ?? 0;
    internal List<TimeWindow> TimeWindows;
    internal TimeSpan? MinimumRuntime;
    internal TimeSpan? MaximumRuntime;
    internal TimeSpan? MinimumTimeout;
    internal TimeSpan? MaximumTimeout;
    internal double SwitchOnLoad;
    internal double SwitchOffLoad;
    internal List<string> ConsumerGroups;

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }
    public IDisposable? StopTimer { get; set; }

    protected EnergyConsumer(EnergyManagerContext context, EnergyConsumerConfiguration config)
    {
        Context = context;
        Name = config.Name;
        TimeWindows = config.TimeWindows.Select(x => new TimeWindow(x.ActiveSensor, new TimeOnly(0, 0).Add(x.Start), new TimeOnly(0, 0).Add(x.End))).ToList(); //TODO clean up

        MinimumRuntime = config.MinimumRuntime;
        MaximumRuntime = config.MaximumRuntime;
        MinimumTimeout = config.MinimumTimeout;
        MaximumTimeout = config.MaximumTimeout;
        ConsumerGroups = config.ConsumerGroups;
        SwitchOnLoad = config.SwitchOnLoad;
        SwitchOffLoad = config.SwitchOffLoad;
    }
    public static async Task<EnergyConsumer> Create(EnergyManagerContext context, EnergyConsumerConfiguration config)
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
        else
            throw new NotSupportedException($"The energy consumer type '{config.GetType().Name}' is not supported.");

        if (context.DebounceDuration != TimeSpan.Zero)
            consumer.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(context.DebounceDuration);

        await consumer.MqttSensors.CreateOrUpdateEntities(config.ConsumerGroups);
        consumer.GetAndSanitizeState();
        consumer.StopIfPastRuntime();
        consumer.StopOnBootIfEnergyIsNoLongerNeeded();
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

        Context.Logger.LogDebug("{Name}: Retrieved state", Name);
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
    public abstract bool CanStart();
    public abstract bool CanForceStop();
    public abstract bool CanForceStopOnPeakLoad();
    public abstract void TurnOn();
    public abstract void TurnOff();
    public abstract void Dispose();
}