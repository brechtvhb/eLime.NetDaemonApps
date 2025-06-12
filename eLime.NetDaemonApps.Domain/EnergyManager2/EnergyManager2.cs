using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.AllowBatteryPower;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.EnergyManager2;

public class EnergyManager2 : IDisposable
{
    private static readonly object _lock = new();

    internal IHaContext HaContext { get; private set; }
    internal ILogger Logger { get; private set; }
    internal IScheduler Scheduler { get; private set; }
    internal IFileStorage FileStorage { get; private set; }

    internal EnergyManagerState State { get; private set; }
    internal EnergyManagerHomeAssistantEntities HomeAssistant { get; private set; }
    internal EnergyManagerMqttSensors MqttSensors { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }
    private DebounceDispatcher? ManageConsumersDebounceDispatcher { get; set; }

    internal GridMonitor2 GridMonitor { get; set; }
    internal List<EnergyConsumer2> Consumers { get; } = [];
    private readonly TimeSpan _minimumChangeInterval = TimeSpan.FromSeconds(20);

    private DateTimeOffset _lastChange = DateTimeOffset.MinValue;
    private IDisposable? GuardTask { get; set; }


    private EnergyManager2()
    {

    }
    public static async Task<EnergyManager2> Create(EnergyManagerConfiguration configuration)
    {
        var energyManager = new EnergyManager2();
        await energyManager.Initialize(configuration);
        return energyManager;
    }

    private async Task Initialize(EnergyManagerConfiguration configuration)
    {
        HaContext = configuration.HaContext;
        Logger = configuration.Logger;
        Scheduler = configuration.Scheduler;
        FileStorage = configuration.FileStorage;

        HomeAssistant = new EnergyManagerHomeAssistantEntities(configuration);

        GridMonitor = GridMonitor2.Create(configuration);
        foreach (var x in configuration.Consumers)
        {
            var consumer = await EnergyConsumer2.Create(Logger, FileStorage, Scheduler, configuration.MqttEntityManager, configuration.Timezone, x);
            consumer.StateChanged += EnergyConsumer_StateChanged;
            Consumers.Add(consumer);
        }

        MqttSensors = new EnergyManagerMqttSensors(Scheduler, configuration.MqttEntityManager);
        if (configuration.DebounceDuration != TimeSpan.Zero)
            ManageConsumersDebounceDispatcher = new DebounceDispatcher(configuration.DebounceDuration);

        SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));

        await MqttSensors.CreateOrUpdateEntities();
        GetAndSanitizeState();
        await SaveAndPublishState();

        GuardTask = Scheduler.RunEvery(TimeSpan.FromSeconds(5), Scheduler.Now, () =>
        {
            if (_lastChange.Add(_minimumChangeInterval) > Scheduler.Now)
                return;

            foreach (var consumer in Consumers)
                consumer.CheckDesiredState(Scheduler.Now);

            DebounceManageConsumers();
        });
    }


    private async Task DebounceSaveAndPublishState()
    {
        if (SaveAndPublishStateDebounceDispatcher == null)
        {
            await SaveAndPublishState();
            return;
        }

        await SaveAndPublishStateDebounceDispatcher.DebounceAsync(SaveAndPublishState);
    }
    internal void DebounceManageConsumers()
    {
        if (ManageConsumersDebounceDispatcher == null)
        {
            ManageConsumersIfNeeded();
            return;
        }

        ManageConsumersDebounceDispatcher.Debounce(ManageConsumersIfNeeded);
    }

    private void ManageConsumersIfNeeded()
    {
        if (!Monitor.TryEnter(_lock))
        {
            Logger.LogInformation("Could not manage consumers because lock object is still locked.");
            return;
        }

        try
        {
            var dynamicNetChange = AdjustDynamicLoadsIfNeeded();
            var startNetChange = StartConsumersIfNeeded(dynamicNetChange);
            var stopNetChange = StopConsumersIfNeeded(dynamicNetChange, startNetChange);
            ManageBatteriesIfNeeded();

            if (dynamicNetChange != 0 || startNetChange != 0 || stopNetChange != 0)
                _lastChange = Scheduler.Now;
        }
        finally
        {
            Monitor.Exit(_lock);
        }

    }

    private Double AdjustDynamicLoadsIfNeeded()
    {
        var dynamicLoadConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.Running).OfType<IDynamicLoadConsumer2>().ToList();
        var dynamicNetChange = 0d;

        foreach (var dynamicLoadConsumer in dynamicLoadConsumers)
        {
            var (current, netChange) = dynamicLoadConsumer.Rebalance(GridMonitor, dynamicNetChange);

            if (netChange == 0)
                continue;

            Logger.LogDebug("{Consumer}: Changed current for dynamic consumer, to {DynamicCurrent}A (Net change: {NetLoadChange}W).", dynamicLoadConsumer.Name, current, netChange);
            dynamicNetChange += netChange;
        }

        return dynamicNetChange;
    }


    //TODO: Use linear programming model and estimates of production and consumption to be able to schedule deferred loads in the future.
    private Double StartConsumersIfNeeded(Double dynamicLoadNetChange)
    {
        var preStartEstimatedLoad = GridMonitor.CurrentLoadMinusBatteries + dynamicLoadNetChange;
        var preStartEstimatedAveragedLoad = GridMonitor.AverageLoadMinusBatteriesSince(Scheduler.Now, _minimumChangeInterval) + dynamicLoadNetChange;
        var startNetChange = 0d;

        var runningConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.Running).ToList();
        //Keep remaining peak load for running consumers in mind (eg: to avoid turning on devices when washer is prewashing but still has to heat).
        var expectedLoad = Math.Round(runningConsumers.Where(x => x.PeakLoad > x.CurrentLoad).Sum(x => (x.PeakLoad - x.CurrentLoad)), 0);
        var estimatedLoad = preStartEstimatedLoad + expectedLoad;
        var estimatedAverageLoad = preStartEstimatedAveragedLoad + expectedLoad;

        var consumersThatCriticallyNeedEnergy = Consumers.Where(x => x is { State.State: EnergyConsumerState.CriticallyNeedsEnergy });

        foreach (var criticalConsumer in consumersThatCriticallyNeedEnergy)
        {
            if (!criticalConsumer.CanStart(Scheduler.Now))
                continue;

            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(criticalConsumer, dynamicLoadNetChange);

            //Will not turn on a load that would exceed current grid import peak
            if (estimatedAverageLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;
            if (estimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;

            criticalConsumer.TurnOn();

            Logger.LogDebug("{Consumer}: Started consumer, consumer is in critical need of energy. Current load/estimated load (dynamicLoadThatCanBeScaledDownOnBehalfOf) was: {CurrentLoad}/{EstimatedLoad} ({DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}.", criticalConsumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, criticalConsumer.SwitchOnLoad, criticalConsumer.PeakLoad);
            estimatedLoad += criticalConsumer.PeakLoad;
            estimatedAverageLoad += criticalConsumer.PeakLoad;
            startNetChange += criticalConsumer.PeakLoad;
        }

        var consumersThatNeedEnergy = Consumers.Where(x => x is { State.State: EnergyConsumerState.NeedsEnergy });
        foreach (var consumer in consumersThatNeedEnergy)
        {
            if (!consumer.CanStart(Scheduler.Now))
                continue;

            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);

            if (consumer is IDynamicLoadConsumer2)
            {
                if (preStartEstimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf >= consumer.SwitchOnLoad)
                    continue;
                if (preStartEstimatedAveragedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf >= consumer.SwitchOnLoad)
                    continue;
            }
            else
            {
                //Will not turn on a consumer when it is below the allowed switch on load
                if (estimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf >= consumer.SwitchOnLoad)
                    continue;
                if (estimatedAverageLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf >= consumer.SwitchOnLoad)
                    continue;
            }

            consumer.TurnOn();

            Logger.LogDebug("{Consumer}: Will start consumer. Current load/estimated (expectedLoad/dynamicLoadThatCanBeScaledDownOnBehalfOf) load was: {CurrentLoad}/{EstimatedLoad} ({ExpectedLoad}/{DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}. Last change was at: {LastChange}", consumer.Name, GridMonitor.CurrentLoad, estimatedLoad, expectedLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOnLoad, consumer.PeakLoad, _lastChange.ToString("O"));
            estimatedLoad += consumer.PeakLoad;
            estimatedAverageLoad += consumer.PeakLoad;
            startNetChange += consumer.PeakLoad;
        }

        return startNetChange;
    }

    private Double StopConsumersIfNeeded(Double dynamicLoadNetChange, Double startLoadNetChange)
    {
        var estimatedLoad = GridMonitor.CurrentLoadMinusBatteries + dynamicLoadNetChange + startLoadNetChange;
        var estimatedAverageLoad = GridMonitor.AverageLoadMinusBatteriesSince(Scheduler.Now, TimeSpan.FromMinutes(3)) + dynamicLoadNetChange + startLoadNetChange;
        var stopNetChange = 0d;

        var consumersThatNoLongerNeedEnergy = Consumers.Where(x => x is { State.State: EnergyConsumerState.Off, IsRunning: true });
        foreach (var consumer in consumersThatNoLongerNeedEnergy)
        {
            Logger.LogDebug("{Consumer}: Will stop consumer because it no longer needs energy.", consumer.Name);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
            estimatedAverageLoad -= consumer.CurrentLoad;
            stopNetChange -= consumer.CurrentLoad;
        }

        var consumersThatPreferSolar = Consumers.OrderByDescending(x => x.SwitchOffLoad).Where(x => x.IsRunning).Where(x => x.CanForceStop(Scheduler.Now)).ToList();
        foreach (var consumer in consumersThatPreferSolar)
        {
            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);

            if (consumer.SwitchOffLoad > estimatedAverageLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf)
                continue;

            if (consumer.SwitchOffLoad > estimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf)
                continue;

            Logger.LogDebug("{Consumer}: Will stop consumer because current load is above switch off load. Current load/estimated (dynamicLoadThatCanBeScaledDownOnBehalfOf) load was: {CurrentLoad}/{EstimatedLoad} ({DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOffLoad, consumer.PeakLoad);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
            estimatedAverageLoad -= consumer.CurrentLoad;
            stopNetChange -= consumer.CurrentLoad;
        }

        if (estimatedAverageLoad < GridMonitor.PeakLoad)
            return stopNetChange;

        if (estimatedLoad < GridMonitor.PeakLoad)
            return stopNetChange;

        //Should we be able to differentiate between force stops and regular stops when managing dynamic loads?
        var dynamicLoadThatCanBeScaledDownOnBehalfOfForAllConsumers = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(null, dynamicLoadNetChange);
        if (dynamicLoadThatCanBeScaledDownOnBehalfOfForAllConsumers > 0)
            return stopNetChange;

        var consumersThatShouldForceStopped = Consumers.Where(x => x.CanForceStopOnPeakLoad(Scheduler.Now) && x.IsRunning);
        foreach (var consumer in consumersThatShouldForceStopped)
        {
            if (consumer is IDynamicLoadConsumer2 && dynamicLoadNetChange != 0)
            {
                Logger.LogDebug("{Consumer}: Should force stop, but won't do it because dynamic load was adjusted. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
                continue;
            }

            Logger.LogDebug("{Consumer}: Will stop consumer right now because peak load was exceeded. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
            estimatedAverageLoad -= consumer.CurrentLoad;
            stopNetChange -= consumer.CurrentLoad;

            if (estimatedAverageLoad <= GridMonitor.PeakLoad)
                break;

            if (estimatedLoad <= GridMonitor.PeakLoad)
                break;
        }


        return stopNetChange;
    }

    private void ManageBatteriesIfNeeded()
    {
        var runningDynamicLoadConsumers = Consumers.Where(x => x.IsRunning).OfType<IDynamicLoadConsumer2>().ToList();

        var canDischarge = runningDynamicLoadConsumers.Count == 0 || runningDynamicLoadConsumers.Any(x => x.AllowBatteryPower == AllowBatteryPower.Yes);
        //if (canDischarge)
        //{
        //    foreach (var battery in Batteries.Where(battery => !battery.CanDischarge))
        //        battery.EnableDischarging();
        //}
        //else
        //{
        //    foreach (var battery in Batteries.Where(battery => battery.CanDischarge))
        //        battery.DisableDischarging();
        //}
    }

    private double GetDynamicLoadThatCanBeScaledDownOnBehalfOf(EnergyConsumer2? consumer, Double dynamicLoadNetChange)
    {
        var consumerGroups = consumer?.ConsumerGroups ?? [];

        if (!consumerGroups.Contains(IDynamicLoadConsumer.CONSUMER_GROUP_ALL))
            consumerGroups.Add(IDynamicLoadConsumer.CONSUMER_GROUP_ALL);

        var dynamicLoadThatCanBeScaledDownOnBehalfOf = Consumers
            .Where(x => x.State.State == EnergyConsumerState.Running)
            .OfType<IDynamicLoadConsumer2>()
            .Where(x => consumerGroups.Contains(x.BalanceOnBehalfOf))
            .Sum(x => x.ReleasablePowerWhenBalancingOnBehalfOf) + dynamicLoadNetChange;

        return Math.Round(dynamicLoadThatCanBeScaledDownOnBehalfOf < 0 ? 0 : dynamicLoadThatCanBeScaledDownOnBehalfOf);
    }

    private async void EnergyConsumer_StateChanged(object? sender, EnergyConsumer2StateChangedEvent e)
    {
        try
        {
            var energyConsumer = Consumers.Single(x => x.Name == e.Consumer.Name);

            Logger.LogInformation("{EnergyConsumer}: State changed to: {State}.", e.Consumer.Name, e.State);

            switch (e)
            {
                case EnergyConsumer2StartCommand:
                    break;
                case EnergyConsumer2StartedEvent:
                    energyConsumer.Started(Scheduler);
                    break;
                case EnergyConsumer2StopCommand:
                    energyConsumer.Stop();
                    break;
                case EnergyConsumer2StoppedEvent:
                    energyConsumer.Stopped(Scheduler.Now);
                    break;
            }

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{EnergyConsumer}: Error while processing state change.", e.Consumer.Name);
        }
    }
    private void GetAndSanitizeState()
    {
        var persistedState = FileStorage.Get<EnergyManagerState>("EnergyManager", "EnergyManager"); //TODO: per battery state is done with consumer?

        State = persistedState ?? new EnergyManagerState();
    }

    private async Task SaveAndPublishState()
    {
        FileStorage.Save("EnergyManager", "EnergyManager", State);
        await MqttSensors.PublishState(State);
    }

    public void Dispose()
    {
        HomeAssistant.Dispose();
        GuardTask?.Dispose();

        foreach (var consumer in Consumers)
            consumer.Dispose();
    }
}