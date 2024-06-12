using eLime.NetDaemonApps.Domain.Entities.Services;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManager : IDisposable
{
    public GridMonitor GridMonitor { get; set; }
    public NumericEntity SolarProductionRemainingTodaySensor { get; }

    public String? PhoneToNotify { get; }
    public Service Services { get; }

    public List<EnergyConsumer> Consumers { get; }
    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private IDisposable? GuardTask { get; }

    public EnergyConsumerState State => Consumers.Any(x => x.State == EnergyConsumerState.CriticallyNeedsEnergy)
        ? EnergyConsumerState.CriticallyNeedsEnergy
        : Consumers.Any(x => x.State == EnergyConsumerState.NeedsEnergy)
            ? EnergyConsumerState.NeedsEnergy
            : Consumers.Any(x => x.State == EnergyConsumerState.Running)
                ? EnergyConsumerState.Running
                : EnergyConsumerState.Off;

    public EnergyManager(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, GridMonitor gridMonitor, NumericEntity solarProductionRemainingTodaySensor, List<EnergyConsumer> energyConsumers, string? phoneToNotify, TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        GridMonitor = gridMonitor;
        SolarProductionRemainingTodaySensor = solarProductionRemainingTodaySensor;

        Services = new Service(_haContext);
        PhoneToNotify = phoneToNotify;

        Consumers = energyConsumers;

        InitializeStateSensor().RunSync();
        foreach (var energyConsumer in Consumers)
        {
            InitializeState(energyConsumer);
            InitializeConsumerSensors(energyConsumer).RunSync();
            InitializeBalancingModeDropDown(energyConsumer).RunSync();
            energyConsumer.StateChanged += EnergyConsumer_StateChanged;
        }

        if (debounceDuration != TimeSpan.Zero)
        {
            ManageConsumersDebounceDispatcher = new DebounceDispatcher(debounceDuration);
            UpdateInHomeAssistantDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));
        }

        GuardTask = _scheduler.RunEvery(TimeSpan.FromSeconds(30), _scheduler.Now, () =>
        {
            foreach (var consumer in Consumers)
                consumer.CheckDesiredState(_scheduler.Now);

            DebounceManageConsumers();
        });

        foreach (var consumer in Consumers)
        {
            consumer.CheckDesiredState(_scheduler.Now);

            if (consumer is { State: EnergyConsumerState.Running, StartedAt: null })
                consumer.Started(_scheduler);
        }
    }


    private void EnergyConsumer_StateChanged(object? sender, EnergyConsumerStateChangedEvent e)
    {
        var energyConsumer = Consumers.Single(x => x.Name == e.Consumer.Name);

        _logger.LogInformation("{EnergyConsumer}: State changed to: {State}.", e.Consumer.Name, e.State);

        switch (e)
        {
            case EnergyConsumerStartCommand:
                break;
            case EnergyConsumerStartedEvent:
                energyConsumer.Started(_scheduler);
                break;
            case EnergyConsumerStopCommand:
                energyConsumer.Stop();
                break;
            case EnergyConsumerStoppedEvent:
                energyConsumer.Stopped(_scheduler.Now);
                break;
        }

        DebounceUpdateInHomeAssistant(energyConsumer).RunSync();
    }

    private void ManageConsumersIfNeeded()
    {
        var estimatedLoad = GridMonitor.CurrentLoad;
        var (estimatedAdjustedLoad, loadAdjusted) = AdjustDynamicLoadsIfNeeded(estimatedLoad);
        StartConsumersIfNeeded(estimatedAdjustedLoad);
        StopConsumersIfNeeded(loadAdjusted);
    }

    private (Double estimatedAdjustedLoad, Boolean loadAdjusted) AdjustDynamicLoadsIfNeeded(Double estimatedLoad)
    {
        var dynamicLoadConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running).OfType<IDynamicLoadConsumer>().ToList();
        var loadAdjusted = false;

        foreach (var dynamicLoadConsumer in dynamicLoadConsumers)
        {
            var (current, netChange) = dynamicLoadConsumer.Rebalance(estimatedLoad, GridMonitor.PeakLoad);

            if (netChange == 0)
                continue;

            _logger.LogDebug("{Consumer}: Changed current for dynamic consumer, to {DynamicCurrent}A (Net change: {NetLoadChange}W).", dynamicLoadConsumer.Name, current, netChange);
            estimatedLoad += netChange;
            loadAdjusted = true;
        }

        return (estimatedLoad, loadAdjusted);
    }

    //TODO: Use linear programming model and estimates of production and consumption to be able to schedule deferred loads in the future.
    private void StartConsumersIfNeeded(Double currentLoad)
    {
        var runningConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running);
        //Keep remaining peak load for running consumers in mind (eg: to avoid turning on devices when washer is prewashing but still has to heat).
        var estimatedLoad = currentLoad + Math.Round(runningConsumers.Where(x => x.PeakLoad > x.CurrentLoad).Sum(x => (x.PeakLoad - x.CurrentLoad)), 0);

        var consumersThatCriticallyNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.CriticallyNeedsEnergy });

        foreach (var criticalConsumer in consumersThatCriticallyNeedEnergy)
        {
            if (!criticalConsumer.CanStart(_scheduler.Now))
                continue;

            //Will not turn on a load that would exceed current grid import peak
            if (estimatedLoad + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;

            criticalConsumer.TurnOn();

            _logger.LogDebug("{Consumer}: Started consumer, consumer is in critical need of energy. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}.", criticalConsumer.Name, currentLoad, estimatedLoad, criticalConsumer.SwitchOnLoad, criticalConsumer.PeakLoad);
            estimatedLoad += criticalConsumer.PeakLoad;
            currentLoad += criticalConsumer.PeakLoad;
        }

        var consumersThatNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.NeedsEnergy });
        foreach (var consumer in consumersThatNeedEnergy)
        {
            if (!consumer.CanStart(_scheduler.Now))
                continue;

            if (consumer is IDynamicLoadConsumer)
            {
                if (currentLoad >= consumer.SwitchOnLoad)
                    continue;
            }
            else
            {
                //Will not turn on a consumer when it is below the allowed switch on load
                if (estimatedLoad >= consumer.SwitchOnLoad)
                    continue;
            }

            consumer.TurnOn();

            _logger.LogDebug("{Consumer}: Will start consumer. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}.", consumer.Name, currentLoad, estimatedLoad, consumer.SwitchOnLoad, consumer.PeakLoad);
            estimatedLoad += consumer.PeakLoad;
            currentLoad += consumer.PeakLoad;
        }
    }

    private void StopConsumersIfNeeded(Boolean dynamicLoadAdjusted)
    {
        var currentLoad = GridMonitor.AverageLoadSince(_scheduler.Now, TimeSpan.FromMinutes(3));
        var estimatedLoad = currentLoad;

        var consumersThatNoLongerNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.Off, Running: true });
        foreach (var consumer in consumersThatNoLongerNeedEnergy)
        {
            _logger.LogDebug("{Consumer}: Will stop consumer because it no longer needs energy.", consumer.Name);
            consumer.Stop();
        }


        var consumersThatPreferSolar = Consumers.OrderByDescending(x => x.SwitchOffLoad).Where(x => x.CanForceStop(_scheduler.Now) && x is { Running: true } && x.SwitchOffLoad < estimatedLoad).ToList();
        foreach (var consumer in consumersThatPreferSolar.TakeWhile(consumer => consumer.SwitchOffLoad < estimatedLoad))
        {
            if (consumer is IDynamicLoadConsumer && dynamicLoadAdjusted)
            {
                _logger.LogDebug("{Consumer}: Should stop, but won't do it because dynamic load was adjusted. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, currentLoad, estimatedLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
                return;
            }

            _logger.LogDebug("{Consumer}: Will stop consumer because current load is above switch off load. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, currentLoad, estimatedLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
        }


        if (estimatedLoad > GridMonitor.PeakLoad)
        {
            var consumersThatShouldForceStopped = Consumers.Where(x => x.CanForceStopOnPeakLoad(_scheduler.Now) && x.Running);
            foreach (var consumer in consumersThatShouldForceStopped)
            {
                if (consumer is IDynamicLoadConsumer && dynamicLoadAdjusted)
                {
                    _logger.LogDebug("{Consumer}: Should force stop, but won't do it because dynamic load was adjusted. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, currentLoad, estimatedLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
                    return;
                }

                _logger.LogDebug("{Consumer}: Will stop consumer right now because peak load was exceeded. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, currentLoad, estimatedLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
                consumer.Stop();
                estimatedLoad -= consumer.CurrentLoad;

                if (estimatedLoad <= GridMonitor.PeakLoad)
                    break;
            }
        }
    }

    private readonly DebounceDispatcher? ManageConsumersDebounceDispatcher;
    private readonly DebounceDispatcher? UpdateInHomeAssistantDebounceDispatcher;

    internal void DebounceManageConsumers()
    {
        if (ManageConsumersDebounceDispatcher == null)
        {
            ManageConsumersIfNeeded();
            return;
        }

        ManageConsumersDebounceDispatcher.Debounce(ManageConsumersIfNeeded);
    }

    private async Task InitializeStateSensor()
    {
        var stateName = $"sensor.energy_manager_state";
        var state = _haContext.Entity(stateName).State;

        if (state == null)
        {
            _logger.LogDebug("Creating Energy manager state sensor in home assistant.");
            var entityOptions = new EnumSensorOptions { Icon = "fapro:square-bolt", Device = GetGlobalDevice(), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(DeviceClass: "enum", UniqueId: stateName, Name: $"Energy manager state", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, State.ToString());

        }
    }


    private async Task InitializeConsumerSensors(EnergyConsumer consumer)
    {
        _logger.LogDebug("{Consumer}: Initializing", consumer.Name);

        var baseName = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}";
        var state = _haContext.Entity($"{baseName}_state").State;

        if (state == null)
        {
            _logger.LogDebug("{Consumer}: Creating energy consumer sensors in home assistant.", consumer.Name);

            var stateOptions = new EnumSensorOptions { Icon = "fapro:bolt-auto", Device = GetConsumerDevice(consumer), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_state", new EntityCreationOptions(DeviceClass: "enum", UniqueId: $"{baseName}_state", Name: $"Consumer {consumer.Name} - state", Persist: true), stateOptions);

            var startedAtOptions = new EntityOptions { Icon = "mdi:calendar-start-outline", Device = GetConsumerDevice(consumer) };
            await _mqttEntityManager.CreateAsync($"{baseName}_started_at", new EntityCreationOptions(UniqueId: $"{baseName}_started_at", Name: $"Consumer {consumer.Name} - Started at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

            var lastRunOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetConsumerDevice(consumer) };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_run", new EntityCreationOptions(UniqueId: $"{baseName}_last_run", Name: $"Consumer {consumer.Name} - Last run", DeviceClass: "timestamp", Persist: true), lastRunOptions);
        }
    }

    private async Task InitializeBalancingModeDropDown(EnergyConsumer consumer)
    {
        if (consumer is not IDynamicLoadConsumer dynamicLoadConsumer)
            return;

        var selectName = $"select.energy_consumer_{consumer.Name.MakeHaFriendly()}_balancing_method";

        var selectOptions = new SelectOptions
        {
            Icon = "mdi:car-turbocharger",
            Options = Enum<BalancingMethod>.AllValuesAsStringList(),
            Device = GetConsumerDevice(consumer)
        };

        await _mqttEntityManager.CreateAsync(selectName, new EntityCreationOptions(UniqueId: selectName, Name: $"Dynamic load balancing method - {consumer.Name}", DeviceClass: "select", Persist: true), selectOptions);
        await _mqttEntityManager.SetStateAsync(selectName, dynamicLoadConsumer.BalancingMethod.ToString());

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(selectName);
        dynamicLoadConsumer.BalancingMethodChangedCommandHandler = observer.SubscribeAsync(SetBalancingMethodHandler(consumer, dynamicLoadConsumer, selectName));
    }

    private Func<string, Task> SetBalancingMethodHandler(EnergyConsumer consumer, IDynamicLoadConsumer dynamicLoadConsumer, string selectName)
    {
        return async state =>
        {
            _logger.LogDebug("{Consumer}: Setting dynamic load balancing method to {State}.", consumer.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            dynamicLoadConsumer.SetBalancingMethod(_scheduler.Now, Enum<BalancingMethod>.Cast(state));
            DebounceUpdateInHomeAssistant(consumer).RunSync();
        };
    }

    private void InitializeState(EnergyConsumer consumer)
    {
        var storedEnergyConsumerState = _fileStorage.Get<EnergyConsumerFileStorage>("EnergyManager", $"{consumer.Name.MakeHaFriendly()}");

        if (storedEnergyConsumerState == null)
            return;

        consumer.SetState(_scheduler, storedEnergyConsumerState.State, storedEnergyConsumerState.StartedAt, storedEnergyConsumerState.LastRun);

        if (consumer is not IDynamicLoadConsumer dynamicLoadConsumer || storedEnergyConsumerState.BalancingMethod is null)
            return;

        dynamicLoadConsumer.SetBalancingMethod(_scheduler.Now, storedEnergyConsumerState.BalancingMethod ?? BalancingMethod.SolarOnly);
    }

    public eLime.NetDaemonApps.Domain.Mqtt.Device GetGlobalDevice()
    {
        return new eLime.NetDaemonApps.Domain.Mqtt.Device { Identifiers = new List<string> { $"energy_manager" }, Name = "Energy manager", Manufacturer = "Me" };
    }

    public eLime.NetDaemonApps.Domain.Mqtt.Device GetConsumerDevice(EnergyConsumer consumer)
    {
        return new eLime.NetDaemonApps.Domain.Mqtt.Device { Identifiers = new List<string> { $"energy_consumer_{consumer.Name.MakeHaFriendly()}" }, Name = "Energy consumer: " + consumer.Name, Manufacturer = "Me" };
    }

    private async Task DebounceUpdateInHomeAssistant(EnergyConsumer? changedConsumer = null)
    {
        if (UpdateInHomeAssistantDebounceDispatcher == null)
        {
            await UpdateStateInHomeAssistant(changedConsumer);
            return;
        }

        await UpdateInHomeAssistantDebounceDispatcher.DebounceAsync(() => UpdateStateInHomeAssistant(changedConsumer));
    }

    private async Task UpdateStateInHomeAssistant(EnergyConsumer? changedConsumer = null)
    {
        var globalAttributes = new EnergyManagerAttributes()
        {
            LastUpdated = DateTime.Now.ToString("O"),
            RunningConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running).Select(x => x.Name).ToList(),
            NeedEnergyConsumers = Consumers.Where(x => x.State == EnergyConsumerState.NeedsEnergy).Select(x => x.Name).ToList(),
            CriticalNeedEnergyConsumers = Consumers.Where(x => x.State == EnergyConsumerState.CriticallyNeedsEnergy).Select(x => x.Name).ToList()
        };

        await _mqttEntityManager.SetStateAsync("sensor.energy_manager_state", State.ToString());
        await _mqttEntityManager.SetAttributesAsync("sensor.energy_manager_state", globalAttributes);


        foreach (var consumer in Consumers)
        {
            if (changedConsumer != null && consumer.Name != changedConsumer.Name)
                continue;

            var baseName = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}";

            await _mqttEntityManager.SetStateAsync($"{baseName}_state", consumer.State.ToString());
            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", consumer.StartedAt?.ToString("O")!);
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_run", consumer.LastRun?.ToString("O")!);

            _fileStorage.Save("EnergyManager", $"{consumer.Name.MakeHaFriendly()}", consumer.ToFileStorage());

            _logger.LogTrace("{Consumer}: Updated Consumer state sensors in home assistant.", consumer.Name);
        }
    }


    public void Dispose()
    {
        foreach (var consumer in Consumers)
        {
            _logger.LogInformation("Disposing Consumer: {Consumer}", consumer.Name);
            consumer.Dispose();
        }

        GridMonitor.Dispose();
        GuardTask?.Dispose();
    }

}