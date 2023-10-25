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
            InitializeConsumerSensors(energyConsumer).RunSync();
            InitializeState(energyConsumer);
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
                consumer.Started(_logger, _scheduler);
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
                energyConsumer.Started(_logger, _scheduler);
                break;
            case EnergyConsumerStopCommand:
                energyConsumer.Stop();
                break;
            case EnergyConsumerStoppedEvent:
                energyConsumer.Stopped(_logger, _scheduler.Now);
                break;
        }

        UpdateStateInHomeAssistant(energyConsumer).RunSync();
    }

    private void ManageConsumersIfNeeded()
    {
        var estimatedLoad = GridMonitor.CurrentLoad;
        estimatedLoad = AdjustDynamicLoadsIfNeeded(estimatedLoad);
        StartConsumersIfNeeded(estimatedLoad);
        StopConsumersIfNeeded();
    }

    private Double AdjustDynamicLoadsIfNeeded(Double estimatedLoad)
    {
        var dynamicLoadConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running && x is IDynamicLoadConsumer).OfType<IDynamicLoadConsumer>().ToList();

        foreach (var dynamicLoadConsumer in dynamicLoadConsumers)
        {
            var (current, netChange) = dynamicLoadConsumer.Rebalance(estimatedLoad);

            if (netChange == 0)
                continue;

            _logger.LogDebug("{Consumer}: Changed current for dynamic consumer, to {DynamicCurrent}A (Net change: {NetLoadChange}W).", dynamicLoadConsumer.Name, current, netChange);
            estimatedLoad += netChange;
        }

        return estimatedLoad;
    }


    //TODO: Use linear programming model and estimates of production and consumption to be able to schedule deferred loads in the future.
    private void StartConsumersIfNeeded(Double estimatedLoad)
    {
        var runningConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running);
        //Keep remaining peak load for running consumers in mind (eg: to avoid turning on devices when washer is prewashing but still has to heat).
        estimatedLoad += runningConsumers.Where(x => x.PeakLoad > x.CurrentLoad).Sum(x => (x.PeakLoad - x.CurrentLoad));

        var consumersThatCriticallyNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.CriticallyNeedsEnergy });

        foreach (var criticalConsumer in consumersThatCriticallyNeedEnergy)
        {
            if (!criticalConsumer.CanStart(_scheduler.Now))
                continue;

            //Will not turn on a load that would exceed current grid import peak
            if (estimatedLoad + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;

            criticalConsumer.TurnOn();

            _logger.LogDebug("{Consumer}: Started consumer, consumer is in critical need of energy.", criticalConsumer.Name);
            estimatedLoad += criticalConsumer.PeakLoad;
        }

        var consumersThatNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.NeedsEnergy });
        foreach (var consumer in consumersThatNeedEnergy)
        {
            if (!consumer.CanStart(_scheduler.Now))
                continue;

            //Will not turn on a consumer when it is below the allowed switch on load
            if (estimatedLoad >= consumer.SwitchOnLoad)
                continue;

            consumer.TurnOn();

            _logger.LogDebug("{Consumer}: Will start consumer.", consumer.Name);
            estimatedLoad += consumer.PeakLoad;
        }
    }

    private void StopConsumersIfNeeded()
    {
        var estimatedLoad = GridMonitor.AverageLoadSince(_scheduler.Now, TimeSpan.FromMinutes(3));

        var consumersThatNoLongerNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.Off, Running: true });
        foreach (var consumer in consumersThatNoLongerNeedEnergy)
        {
            _logger.LogDebug("{Consumer}: Will stop consumer because it no longer needs energy.", consumer.Name);
            consumer.Stop();
        }


        var consumersThatPreferSolar = Consumers.OrderByDescending(x => x.SwitchOffLoad).Where(x => x.CanForceStop(_scheduler.Now) && x is { Running: true } && x.SwitchOffLoad < estimatedLoad).ToList();
        foreach (var consumer in consumersThatPreferSolar.TakeWhile(consumer => consumer.SwitchOffLoad < estimatedLoad))
        {
            _logger.LogDebug("{Consumer}: Will stop consumer because current load is above switch off load", consumer.Name);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
        }


        if (estimatedLoad > GridMonitor.PeakLoad)
        {
            var consumersThatShouldForceStopped = Consumers.Where(x => x.CanForceStopOnPeakLoad(_scheduler.Now) && x.Running);
            foreach (var consumer in consumersThatShouldForceStopped)
            {
                _logger.LogDebug("{Consumer}: Will stop consumer right now because peak load was exceeded.", consumer.Name);
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
            _logger.LogDebug("{Consumer}: Creating energy consumer state sensor in home assistant. State was '{State}'.", consumer.Name, state);

            var stateOptions = new EnumSensorOptions { Icon = "fapro:bolt-auto", Device = GetConsumerDevice(consumer), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_state", new EntityCreationOptions(DeviceClass: "enum", UniqueId: $"{baseName}_state", Name: $"Consumer {consumer.Name} - state", Persist: true), stateOptions);
            await _mqttEntityManager.SetStateAsync($"{baseName}_state", consumer.State.ToString());

            var startedAtOptions = new EntityOptions { Icon = "mdi:calendar-start-outline", Device = GetConsumerDevice(consumer) };
            await _mqttEntityManager.CreateAsync($"{baseName}_started_at", new EntityCreationOptions(UniqueId: $"{baseName}_started_at", Name: $"Consumer {consumer.Name} - Started at", DeviceClass: "date", Persist: true), startedAtOptions);
            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", consumer.StartedAt?.ToString("O") ?? string.Empty);

            var lastRunOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetConsumerDevice(consumer) };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_run", new EntityCreationOptions(UniqueId: $"{baseName}_last_run", Name: $"Consumer {consumer.Name} - Last run", DeviceClass: "date", Persist: true), lastRunOptions);
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_run", consumer.LastRun?.ToString("O") ?? string.Empty);
        }
    }

    private void InitializeState(EnergyConsumer consumer)
    {
        var storedEnergyConsumerState = _fileStorage.Get<EnergyConsumerFileStorage>("EnergyManager", $"{consumer.Name.MakeHaFriendly()}");

        if (storedEnergyConsumerState == null)
            return;

        consumer.SetState(_logger, _scheduler, storedEnergyConsumerState.State, storedEnergyConsumerState.StartedAt, storedEnergyConsumerState.LastRun);
    }

    public Device GetGlobalDevice()
    {
        return new Device { Identifiers = new List<string> { $"energy_manager" }, Name = "Energy manager", Manufacturer = "Me" };
    }

    public Device GetConsumerDevice(EnergyConsumer consumer)
    {
        return new Device { Identifiers = new List<string> { $"energy_consumer_{consumer.Name.MakeHaFriendly()}" }, Name = "Energy consumer: " + consumer.Name, Manufacturer = "Me" };
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
            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", consumer.StartedAt?.ToString("O") ?? string.Empty);
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_run", consumer.LastRun?.ToString("O") ?? string.Empty);

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