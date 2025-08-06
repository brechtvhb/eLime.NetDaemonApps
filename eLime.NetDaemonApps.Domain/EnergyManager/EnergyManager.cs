using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;
using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using System.Runtime.CompilerServices;
using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.AllowBatteryPower;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManager : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    internal EnergyManagerContext Context { get; private set; }
    internal List<string> ConsumerGroups { get; private set; }
    internal EnergyManagerState State { get; private set; }
    internal EnergyManagerHomeAssistantEntities HomeAssistant { get; private set; }
    internal EnergyManagerMqttSensors MqttSensors { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }
    private DebounceDispatcher? ManageConsumersDebounceDispatcher { get; set; }

    internal GridMonitor GridMonitor { get; set; }
    internal List<EnergyConsumer> Consumers { get; } = [];
    internal BatteryManager.BatteryManager BatteryManager { get; set; }
    private readonly TimeSpan _minimumChangeInterval = TimeSpan.FromSeconds(20);

    private IDisposable? GuardTask { get; set; }

    private EnergyManager()
    {

    }
    public static async Task<EnergyManager> Create(EnergyManagerConfiguration configuration)
    {
        var energyManager = new EnergyManager();
        await energyManager.Initialize(configuration);
        return energyManager;
    }

    private async Task Initialize(EnergyManagerConfiguration configuration)
    {
        Context = configuration.Context;

        ConsumerGroups = [IDynamicLoadConsumer.CONSUMER_GROUP_SELF];
        ConsumerGroups.AddRange(configuration.Consumers.SelectMany(x => x.ConsumerGroups).Distinct());
        ConsumerGroups.Add(IDynamicLoadConsumer.CONSUMER_GROUP_ALL);

        HomeAssistant = new EnergyManagerHomeAssistantEntities(configuration);

        GridMonitor = GridMonitor.Create(configuration);
        foreach (var x in configuration.Consumers)
        {
            var consumer = await EnergyConsumer.Create(Context, ConsumerGroups, x);
            Consumers.Add(consumer);
        }
        BatteryManager = await Domain.EnergyManager.BatteryManager.BatteryManager.Create(Context, configuration.BatteryManager);

        MqttSensors = new EnergyManagerMqttSensors(Context);
        if (Context.DebounceDuration != TimeSpan.Zero)
            ManageConsumersDebounceDispatcher = new DebounceDispatcher(Context.DebounceDuration);

        if (Context.DebounceDuration != TimeSpan.Zero)
            SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(Context.DebounceDuration);

        await MqttSensors.CreateOrUpdateEntities();
        GetAndSanitizeState();
        await SaveAndPublishState();

        GuardTask = Context.Scheduler.RunEvery(TimeSpan.FromSeconds(5), Context.Scheduler.Now, async void () =>
        {
            try
            {
                if (State.LastChange.Add(_minimumChangeInterval) > Context.Scheduler.Now)
                    return;

                foreach (var consumer in Consumers)
                    consumer.UpdateState();

                await DebounceManageConsumers();
            }
            catch (Exception ex)
            {
                Context.Logger.LogError(ex, "Fatal excepting when trying to manage consumers");
            }
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
    internal async Task DebounceManageConsumers()
    {
        if (ManageConsumersDebounceDispatcher == null)
        {
            await ManageConsumersIfNeeded();
            return;
        }

        ManageConsumersDebounceDispatcher.Debounce(() => _ = ManageConsumersIfNeeded());
    }

    private async Task ManageConsumersIfNeeded()
    {
        if (!await _semaphore.WaitAsync(0))
        {
            Context.Logger.LogInformation("Could not manage consumers because lock object is still locked.");
            return;
        }

        try
        {
            var consumerLoadCorrections = CalculateConsumerLoadCorrections();
            var dynamicNetChange = AdjustDynamicLoadsIfNeeded(consumerLoadCorrections);
            var startNetChange = StartConsumersIfNeeded(consumerLoadCorrections, dynamicNetChange);
            var stopNetChange = StopConsumersIfNeeded(consumerLoadCorrections, dynamicNetChange, startNetChange);
            await ManageBatteriesIfNeeded();
            await UpdateStateIfSomethingChanged(dynamicNetChange, startNetChange, stopNetChange);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error while managing consumers.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private Dictionary<LoadTimeFrames, double> CalculateConsumerLoadCorrections()
    {
        var correctedLoads = new Dictionary<LoadTimeFrames, double>
        {
            { LoadTimeFrames.Now, 0.0 },
            { LoadTimeFrames.Last30Seconds, Consumers.Select(x => x.AverageLoadCorrection(TimeSpan.FromSeconds(30))).Sum() },
            { LoadTimeFrames.LastMinute, Consumers.Select(x => x.AverageLoadCorrection(TimeSpan.FromMinutes(1))).Sum() },
            { LoadTimeFrames.Last2Minutes, Consumers.Select(x => x.AverageLoadCorrection(TimeSpan.FromMinutes(2))).Sum() },
            { LoadTimeFrames.Last5Minutes, Consumers.Select(x => x.AverageLoadCorrection(TimeSpan.FromMinutes(5))).Sum() }
        };

        return correctedLoads;
    }

    private double AdjustDynamicLoadsIfNeeded(Dictionary<LoadTimeFrames, double> consumerLoadCorrections)
    {
        var dynamicLoadConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.Running).OfType<IDynamicLoadConsumer>().ToList();
        var dynamicNetChange = 0d;

        foreach (var dynamicLoadConsumer in dynamicLoadConsumers)
        {
            var (current, netChange) = dynamicLoadConsumer.Rebalance(GridMonitor, consumerLoadCorrections, dynamicNetChange, BatteryManager.MaximumDischargePower);

            if (netChange == 0)
                continue;

            Context.Logger.LogDebug("{Consumer}: Changed current for dynamic consumer, to {DynamicCurrent}A (Net change: {NetLoadChange}W).", dynamicLoadConsumer.Name, current, netChange);
            dynamicNetChange += netChange;
        }

        return dynamicNetChange;
    }

    private double StartConsumersIfNeeded(Dictionary<LoadTimeFrames, double> consumerLoadCorrections, double dynamicLoadNetChange)
    {
        var startNetChange = 0d;

        //Keep remaining peak load for running consumers in mind (eg: to avoid turning on devices when washer is pre-washing but still has to heat).
        var expectedLoad = Math.Round(Consumers.Where(x => x.State.State == EnergyConsumerState.Running && x.PeakLoad > x.CurrentLoad).Sum(x => x.PeakLoad - x.CurrentLoad), 0);

        var consumers = Consumers.Where(x => x.State.State is EnergyConsumerState.NeedsEnergy or EnergyConsumerState.CriticallyNeedsEnergy).OrderByDescending(x => x.State.State);

        foreach (var consumer in consumers)
        {
            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);
            if (!consumer.CanStart(GridMonitor, consumerLoadCorrections, expectedLoad, dynamicLoadNetChange, dynamicLoadThatCanBeScaledDownOnBehalfOf, startNetChange))
                continue;

            consumer.TurnOn();

            Context.Logger.LogDebug("{Consumer}: Started consumer, state is {EnergyConsumerState}. Loads [Current: {CurrentLoad}, CanScaleDownOnBehalfOf: {DynamicLoadThatCanBeScaledDownOnBehalfOf}, ConsumerSwitchOn: {SwitchOnLoad}, ConsumerPeakLoad: {PeakLoad}]", consumer.Name, consumer.State.State, GridMonitor.CurrentLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOnLoad, consumer.PeakLoad);
            startNetChange += consumer.PeakLoad;
        }

        return startNetChange;
    }

    private double StopConsumersIfNeeded(Dictionary<LoadTimeFrames, double> consumerLoadCorrections, double dynamicLoadNetChange, double startLoadNetChange)
    {
        var expectedLoad = Math.Round(Consumers.Where(x => x.State.State == EnergyConsumerState.Running && x.PeakLoad > x.CurrentLoad).Sum(x => x.PeakLoad - x.CurrentLoad), 0);

        var stopNetChange = 0d;

        //TODO: I want to remove this
        var consumersThatNoLongerNeedEnergy = Consumers.Where(x => x is { State.State: EnergyConsumerState.Off, IsRunning: true });
        foreach (var consumer in consumersThatNoLongerNeedEnergy)
        {
            Context.Logger.LogWarning("{Consumer}: Will stop consumer because it no longer needs energy. This should not happen! Means there is a bug in code", consumer.Name);
            consumer.Stop();
            stopNetChange -= consumer.CurrentLoad;
        }

        //We have a dynamic load that can be scaled down on behalf of all consumers, so we're not forcing any shut down of consumers.
        var dynamicLoadThatCanBeScaledDownOnBehalfOfForAllConsumers = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(null, dynamicLoadNetChange);
        if (dynamicLoadThatCanBeScaledDownOnBehalfOfForAllConsumers > 50)
            return stopNetChange;

        var consumersThatCanStop = Consumers.Where(x => x.IsRunning && (x.CanForceStop() || x.CanForceStopOnPeakLoad())).OrderByDescending(x => x.CanForceStop()).ThenBy(x => x.SwitchOffLoad).ToList();
        foreach (var consumer in consumersThatCanStop)
        {
            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);
            if (!consumer.CanStop(GridMonitor, consumerLoadCorrections, expectedLoad, dynamicLoadNetChange, dynamicLoadThatCanBeScaledDownOnBehalfOf, startLoadNetChange, stopNetChange))
                continue;

            var reason = consumer.CanForceStop() ? "it is consuming too much grid power" : "peak load is exceeded";
            Context.Logger.LogDebug("{Consumer}: Will stop because {reason}. Loads [Current: {CurrentLoad}, CanBeScaledDownOnBehalfOf: {DynamicLoadThatCanBeScaledDownOnBehalfOf}, ConsumerSwitchOff: {SwitchOffLoad}, ConsumerPeakLoad: {PeakLoad}]", consumer.Name, reason, GridMonitor.CurrentLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOffLoad, consumer.PeakLoad);
            consumer.Stop();
            stopNetChange -= consumer.CurrentLoad;
        }

        return stopNetChange;
    }

    private async Task ManageBatteriesIfNeeded()
    {
        var runningDynamicLoadConsumers = Consumers.Where(x => x.IsRunning).OfType<IDynamicLoadConsumer>().ToList();
        var averageDischargePower = GridMonitor.AverageBatteriesDischargingPower(TimeSpan.FromMinutes(3));
        await BatteryManager.ManageBatteryPowerSettings(runningDynamicLoadConsumers.Any(), runningDynamicLoadConsumers.Any(x => x.AllowBatteryPower is AllowBatteryPower.MaxPower or AllowBatteryPower.FlattenGridLoad), averageDischargePower);
    }

    private async Task UpdateStateIfSomethingChanged(double dynamicNetChange, double startNetChange, double stopNetChange)
    {
        if (dynamicNetChange != 0 || startNetChange != 0 || stopNetChange != 0)
        {
            State.State = Consumers.Any(x => x.State.State == EnergyConsumerState.CriticallyNeedsEnergy)
                ? EnergyConsumerState.CriticallyNeedsEnergy
                : Consumers.Any(x => x.State.State == EnergyConsumerState.NeedsEnergy)
                    ? EnergyConsumerState.NeedsEnergy
                    : Consumers.Any(x => x.State.State == EnergyConsumerState.Running)
                        ? EnergyConsumerState.Running
                        : EnergyConsumerState.Off;

            State.LastChange = Context.Scheduler.Now;
            State.RunningConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.Running).Select(x => x.Name).ToList();
            State.NeedEnergyConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.NeedsEnergy).Select(x => x.Name).ToList();
            State.CriticalNeedEnergyConsumers = Consumers.Where(x => x.State.State == EnergyConsumerState.CriticallyNeedsEnergy).Select(x => x.Name).ToList();

            await DebounceSaveAndPublishState();
        }
    }

    private double GetDynamicLoadThatCanBeScaledDownOnBehalfOf(EnergyConsumer? consumer, double dynamicLoadNetChange)
    {
        var consumerGroups = consumer?.ConsumerGroups ?? [];

        if (!consumerGroups.Contains(IDynamicLoadConsumer.CONSUMER_GROUP_ALL))
            consumerGroups.Add(IDynamicLoadConsumer.CONSUMER_GROUP_ALL);

        var dynamicLoadThatCanBeScaledDownOnBehalfOf = Consumers
            .Where(x => x.State.State == EnergyConsumerState.Running)
            .OfType<IDynamicLoadConsumer>()
            .Where(x => consumerGroups.Contains(x.BalanceOnBehalfOf))
            .Sum(x => x.ReleasablePowerWhenBalancingOnBehalfOf) + dynamicLoadNetChange;

        return Math.Round(dynamicLoadThatCanBeScaledDownOnBehalfOf);
    }

    private void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<EnergyManagerState>("EnergyManager", "_energy_manager");
        State = persistedState ?? new EnergyManagerState();
    }

    private async Task SaveAndPublishState()
    {
        Context.FileStorage.Save("EnergyManager", "_energy_manager", State);
        await MqttSensors.PublishState(State);
    }

    public void Dispose()
    {
        BatteryManager.Dispose();

        foreach (var consumer in Consumers)
            consumer.Dispose();

        HomeAssistant.Dispose();
        MqttSensors.Dispose();
        GuardTask?.Dispose();
    }
}
