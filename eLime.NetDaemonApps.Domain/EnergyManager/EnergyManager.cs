using eLime.NetDaemonApps.Domain.Entities.Services;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
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

    private IDisposable? GuardTask { get; }

    public EnergyConsumerState State => Consumers.Any(x => x.State == EnergyConsumerState.CriticallyNeedsEnergy)
        ? EnergyConsumerState.CriticallyNeedsEnergy
        : Consumers.Any(x => x.State == EnergyConsumerState.NeedsEnergy)
            ? EnergyConsumerState.NeedsEnergy
            : Consumers.Any(x => x.State == EnergyConsumerState.Running)
                ? EnergyConsumerState.Running
                : EnergyConsumerState.Off;

    public EnergyManager(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, GridMonitor gridMonitor, NumericEntity solarProductionRemainingTodaySensor, List<EnergyConsumer> energyConsumers, string? phoneToNotify, TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;

        GridMonitor = gridMonitor;
        SolarProductionRemainingTodaySensor = solarProductionRemainingTodaySensor;

        Services = new Service(_haContext);
        PhoneToNotify = phoneToNotify;

        Consumers = energyConsumers;

        InitializeStateSensor().RunSync();
        foreach (var energyConsumer in Consumers)
        {
            InitializeConsumerSensor(energyConsumer).RunSync();
            energyConsumer.StateChanged += EnergyConsumer_StateChanged;
        }

        if (debounceDuration != TimeSpan.Zero)
        {
            StartConsumersDebounceDispatcher = new(debounceDuration);
            StopConsumersDebounceDispatcher = new(debounceDuration);
            UpdateInHomeAssistantDebounceDispatcher = new(TimeSpan.FromSeconds(1));
        }

        GuardTask = _scheduler.RunEvery(TimeSpan.FromSeconds(30), _scheduler.Now, () =>
        {
            foreach (var consumer in Consumers)
                consumer.CheckDesiredState(_scheduler.Now);

            DebounceStartConsumers();
            DebounceStopConsumers();
            DebounceUpdateInHomeAssistant().RunSync();
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

        DebounceUpdateInHomeAssistant().RunSync();
    }

    //TODO: Use linear programming model and estimates of production and consumption to be able to schedule deferred loads in the future.
    private void StartConsumersIfNeeded()
    {
        var estimatedLoad = GridMonitor.CurrentLoad;

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

        if (estimatedLoad > 0)
        {
            var consumersThatPreferSolar = Consumers.Where(x => x.CanForceStop(_scheduler.Now) && x is { Running: true, PreferSolar: true });
            foreach (var consumer in consumersThatPreferSolar)
            {
                _logger.LogDebug("{Consumer}: Will stop consumer because it prefers solar energy.", consumer.Name);
                consumer.Stop();
            }
        }

        if (estimatedLoad > GridMonitor.PeakLoad)
        {
            var consumersThatShouldForceStopped = Consumers.Where(x => x.CanForceStop(_scheduler.Now) && x.Running);
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

    private readonly DebounceDispatcher? StartConsumersDebounceDispatcher;
    private readonly DebounceDispatcher? StopConsumersDebounceDispatcher;
    private readonly DebounceDispatcher? UpdateInHomeAssistantDebounceDispatcher;

    internal void DebounceStartConsumers()
    {
        if (StartConsumersDebounceDispatcher == null)
        {
            StartConsumersIfNeeded();
            return;
        }

        StartConsumersDebounceDispatcher.Debounce(StartConsumersIfNeeded);
    }


    internal void DebounceStopConsumers()
    {
        if (StopConsumersDebounceDispatcher == null)
        {
            StopConsumersIfNeeded();
            return;
        }

        StopConsumersDebounceDispatcher.Debounce(StopConsumersIfNeeded);
    }

    private async Task InitializeStateSensor()
    {
        var stateName = $"sensor.energy_manager_state";
        var state = _haContext.Entity(stateName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating Energy manager state sensor in home assistant.");
            var entityOptions = new EnumSensorOptions() { Icon = "fapro:square-bolt", Device = GetGlobalDevice(), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(DeviceClass: "enum", UniqueId: stateName, Name: $"Energy manager state", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, State.ToString());
        }
    }


    private async Task InitializeConsumerSensor(EnergyConsumer consumer)
    {
        var stateName = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_state";

        var state = _haContext.Entity(stateName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("{Consumer}: Creating energy consumer state sensor in home assistant.", consumer.Name);

            var entityOptions = new EnumSensorOptions { Icon = "fapro:bolt-auto", Device = GetConsumerDevice(consumer), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(DeviceClass: "enum", UniqueId: stateName, Name: $"Consumer state - {consumer.Name}", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, consumer.State.ToString());
        }
        else
        {
            consumer.SetState(Enum<EnergyConsumerState>.Cast(state));
            var entity = new Entity<EnergyConsumerAttributes>(_haContext, stateName);

            if (!String.IsNullOrWhiteSpace(entity.Attributes?.LastRun))
                consumer.Stopped(_logger, DateTime.Parse(entity.Attributes.LastRun));

            if (!String.IsNullOrWhiteSpace(entity.Attributes?.StartedAt))
                consumer.Started(_logger, _scheduler, DateTime.Parse(entity.Attributes.StartedAt));
        }

    }

    public Device GetGlobalDevice()
    {
        return new Device { Identifiers = new List<string> { $"energy_manager" }, Name = "Energy manager", Manufacturer = "Me" };
    }

    public Device GetConsumerDevice(EnergyConsumer consumer)
    {
        return new Device { Identifiers = new List<string> { $"energy_consumer_{consumer.Name.MakeHaFriendly()}" }, Name = "Energy consumer: " + consumer.Name, Manufacturer = "Me" };
    }

    private async Task DebounceUpdateInHomeAssistant()
    {
        if (UpdateInHomeAssistantDebounceDispatcher == null)
        {
            await UpdateStateInHomeAssistant();
            return;
        }

        await UpdateInHomeAssistantDebounceDispatcher.DebounceAsync(UpdateStateInHomeAssistant);
    }

    private async Task UpdateStateInHomeAssistant()
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
            var stateName = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_state";

            var attributes = new EnergyConsumerAttributes()
            {
                LastUpdated = DateTime.Now.ToString("O"),
                StartedAt = consumer.StartedAt?.ToString("O"),
                LastRun = consumer.LastRun?.ToString("O"),
                Icon = "fapro:bolt-auto"
            };
            await _mqttEntityManager.SetStateAsync(stateName, consumer.State.ToString());
            await _mqttEntityManager.SetAttributesAsync(stateName, attributes);

            _logger.LogTrace("{Consumer}: Update Consumer state sensor in home assistant (attributes: {Attributes})", consumer.Name, attributes);
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