﻿using eLime.NetDaemonApps.Domain.Entities.Services;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;
using EntityOptions = eLime.NetDaemonApps.Domain.Mqtt.EntityOptions;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManager : IDisposable
{
    private static readonly object _lock = new();

    public IGridMonitor GridMonitor { get; set; }
    public NumericEntity SolarProductionRemainingTodaySensor { get; }

    public String? PhoneToNotify { get; }
    public Service Services { get; }

    public List<EnergyConsumer> Consumers { get; }
    public List<Battery> Batteries { get; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private readonly TimeSpan _minimumChangeInterval = TimeSpan.FromSeconds(30);

    private DateTimeOffset _lastChange = DateTimeOffset.MinValue;
    private IDisposable? GuardTask { get; }

    public EnergyConsumerState State => Consumers.Any(x => x.State == EnergyConsumerState.CriticallyNeedsEnergy)
        ? EnergyConsumerState.CriticallyNeedsEnergy
        : Consumers.Any(x => x.State == EnergyConsumerState.NeedsEnergy)
            ? EnergyConsumerState.NeedsEnergy
            : Consumers.Any(x => x.State == EnergyConsumerState.Running)
                ? EnergyConsumerState.Running
                : EnergyConsumerState.Off;

    public EnergyManager(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IGridMonitor gridMonitor, NumericEntity solarProductionRemainingTodaySensor, List<EnergyConsumer> energyConsumers, List<Battery> batteries, string? phoneToNotify, TimeSpan debounceDuration)
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
        Batteries = batteries;
        InitializeStateSensor().RunSync();
        foreach (var energyConsumer in Consumers)
        {
            InitializeState(energyConsumer);
            InitializeConsumerSensors(energyConsumer).RunSync();
            InitializeDynamicLoadConsumerSensors(energyConsumer).RunSync();
            energyConsumer.StateChanged += EnergyConsumer_StateChanged;
        }

        if (debounceDuration != TimeSpan.Zero)
        {
            ManageConsumersDebounceDispatcher = new DebounceDispatcher(debounceDuration);
            UpdateInHomeAssistantDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));
        }

        GuardTask = _scheduler.RunEvery(TimeSpan.FromSeconds(5), _scheduler.Now, () =>
        {
            if (_lastChange.Add(_minimumChangeInterval) > _scheduler.Now)
                return;

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

        _logger.LogInformation("EnergyManager: Current peak load is: {PeakLoad} W.", gridMonitor.PeakLoad);
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
        if (!Monitor.TryEnter(_lock))
        {
            _logger.LogInformation("Could not manage consumers because lock object is still locked.");
            return;
        }

        try
        {
            var dynamicNetChange = AdjustDynamicLoadsIfNeeded();
            var startNetChange = StartConsumersIfNeeded(dynamicNetChange);
            var stopNetChange = StopConsumersIfNeeded(dynamicNetChange, startNetChange);
            ManageBatteriesIfNeeded();

            if (dynamicNetChange != 0 || startNetChange != 0 || stopNetChange != 0)
                _lastChange = _scheduler.Now;
        }
        finally
        {
            Monitor.Exit(_lock);
        }

    }

    private Double AdjustDynamicLoadsIfNeeded()
    {
        var dynamicLoadConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running).OfType<IDynamicLoadConsumer>().ToList();
        var dynamicNetChange = 0d;

        foreach (var dynamicLoadConsumer in dynamicLoadConsumers)
        {
            var (current, netChange) = dynamicLoadConsumer.Rebalance(GridMonitor, dynamicNetChange);

            if (netChange == 0)
                continue;

            _logger.LogDebug("{Consumer}: Changed current for dynamic consumer, to {DynamicCurrent}A (Net change: {NetLoadChange}W).", dynamicLoadConsumer.Name, current, netChange);
            dynamicNetChange += netChange;
        }

        return dynamicNetChange;
    }

    //TODO: Use linear programming model and estimates of production and consumption to be able to schedule deferred loads in the future.
    private Double StartConsumersIfNeeded(Double dynamicLoadNetChange)
    {
        var preStartEstimatedLoad = GridMonitor.CurrentLoadMinusBatteries + dynamicLoadNetChange;
        var preStartEstimatedAveragedLoad = GridMonitor.AverageLoadMinusBatteriesSince(_scheduler.Now, _minimumChangeInterval) + dynamicLoadNetChange;
        var startNetChange = 0d;

        var runningConsumers = Consumers.Where(x => x.State == EnergyConsumerState.Running).ToList();
        //Keep remaining peak load for running consumers in mind (eg: to avoid turning on devices when washer is prewashing but still has to heat).
        var expectedLoad = Math.Round(runningConsumers.Where(x => x.PeakLoad > x.CurrentLoad).Sum(x => (x.PeakLoad - x.CurrentLoad)), 0);
        var estimatedLoad = preStartEstimatedLoad + expectedLoad;
        var estimatedAverageLoad = preStartEstimatedAveragedLoad + expectedLoad;

        var consumersThatCriticallyNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.CriticallyNeedsEnergy });

        foreach (var criticalConsumer in consumersThatCriticallyNeedEnergy)
        {
            if (!criticalConsumer.CanStart(_scheduler.Now))
                continue;

            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(criticalConsumer, dynamicLoadNetChange);

            //Will not turn on a load that would exceed current grid import peak
            if (estimatedAverageLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;
            if (estimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf + criticalConsumer.PeakLoad > GridMonitor.PeakLoad)
                continue;

            criticalConsumer.TurnOn();

            _logger.LogDebug("{Consumer}: Started consumer, consumer is in critical need of energy. Current load/estimated load (dynamicLoadThatCanBeScaledDownOnBehalfOf) was: {CurrentLoad}/{EstimatedLoad} ({DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}.", criticalConsumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, criticalConsumer.SwitchOnLoad, criticalConsumer.PeakLoad);
            estimatedLoad += criticalConsumer.PeakLoad;
            estimatedAverageLoad += criticalConsumer.PeakLoad;
            startNetChange += criticalConsumer.PeakLoad;
        }

        var consumersThatNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.NeedsEnergy });
        foreach (var consumer in consumersThatNeedEnergy)
        {
            if (!consumer.CanStart(_scheduler.Now))
                continue;

            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);

            if (consumer is IDynamicLoadConsumer)
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

            _logger.LogDebug("{Consumer}: Will start consumer. Current load/estimated (expectedLoad/dynamicLoadThatCanBeScaledDownOnBehalfOf) load was: {CurrentLoad}/{EstimatedLoad} ({ExpectedLoad}/{DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-on/peak load of consumer is: {SwitchOnLoad}/{PeakLoad}. Last change was at: {LastChange}", consumer.Name, GridMonitor.CurrentLoad, estimatedLoad, expectedLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOnLoad, consumer.PeakLoad, _lastChange.ToString("O"));
            estimatedLoad += consumer.PeakLoad;
            estimatedAverageLoad += consumer.PeakLoad;
            startNetChange += consumer.PeakLoad;
        }

        return startNetChange;
    }

    private Double StopConsumersIfNeeded(Double dynamicLoadNetChange, Double startLoadNetChange)
    {
        var estimatedLoad = GridMonitor.CurrentLoadMinusBatteries + dynamicLoadNetChange + startLoadNetChange;
        var estimatedAverageLoad = GridMonitor.AverageLoadMinusBatteriesSince(_scheduler.Now, TimeSpan.FromMinutes(3)) + dynamicLoadNetChange + startLoadNetChange;
        var stopNetChange = 0d;

        var consumersThatNoLongerNeedEnergy = Consumers.Where(x => x is { State: EnergyConsumerState.Off, Running: true });
        foreach (var consumer in consumersThatNoLongerNeedEnergy)
        {
            _logger.LogDebug("{Consumer}: Will stop consumer because it no longer needs energy.", consumer.Name);
            consumer.Stop();
            estimatedLoad -= consumer.CurrentLoad;
            estimatedAverageLoad -= consumer.CurrentLoad;
            stopNetChange -= consumer.CurrentLoad;
        }

        var consumersThatPreferSolar = Consumers.OrderByDescending(x => x.SwitchOffLoad).Where(x => x.Running).Where(x => x.CanForceStop(_scheduler.Now)).ToList();
        foreach (var consumer in consumersThatPreferSolar)
        {
            var dynamicLoadThatCanBeScaledDownOnBehalfOf = GetDynamicLoadThatCanBeScaledDownOnBehalfOf(consumer, dynamicLoadNetChange);

            if (consumer.SwitchOffLoad > estimatedAverageLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf)
                continue;

            if (consumer.SwitchOffLoad > estimatedLoad - dynamicLoadThatCanBeScaledDownOnBehalfOf)
                continue;

            _logger.LogDebug("{Consumer}: Will stop consumer because current load is above switch off load. Current load/estimated (dynamicLoadThatCanBeScaledDownOnBehalfOf) load was: {CurrentLoad}/{EstimatedLoad} ({DynamicLoadThatCanBeScaledDownOnBehalfOf}). Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, dynamicLoadThatCanBeScaledDownOnBehalfOf, consumer.SwitchOffLoad, consumer.PeakLoad);
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

        var consumersThatShouldForceStopped = Consumers.Where(x => x.CanForceStopOnPeakLoad(_scheduler.Now) && x.Running);
        foreach (var consumer in consumersThatShouldForceStopped)
        {
            if (consumer is IDynamicLoadConsumer && dynamicLoadNetChange != 0)
            {
                _logger.LogDebug("{Consumer}: Should force stop, but won't do it because dynamic load was adjusted. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
                continue;
            }

            _logger.LogDebug("{Consumer}: Will stop consumer right now because peak load was exceeded. Current load/estimated load was: {CurrentLoad}/{EstimatedLoad}. Switch-off/peak load of consumer is: {SwitchOffLoad}/{PeakLoad}", consumer.Name, GridMonitor.CurrentLoad, estimatedAverageLoad, consumer.SwitchOffLoad, consumer.PeakLoad);
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
        var runningDynamicLoadConsumers = Consumers.Where(x => x.Running).OfType<IDynamicLoadConsumer>().ToList();

        var canDischarge = runningDynamicLoadConsumers.Count == 0 || runningDynamicLoadConsumers.Any(x => x.AllowBatteryPower == AllowBatteryPower.Yes);
        if (canDischarge)
        {
            foreach (var battery in Batteries.Where(battery => !battery.CanDischarge))
                battery.EnableDischarging();
        }
        else
        {
            foreach (var battery in Batteries.Where(battery => battery.CanDischarge))
                battery.DisableDischarging();
        }
    }

    private double GetDynamicLoadThatCanBeScaledDownOnBehalfOf(EnergyConsumer? consumer, Double dynamicLoadNetChange)
    {
        var consumerGroups = consumer?.ConsumerGroups ?? [];

        if (!consumerGroups.Contains(IDynamicLoadConsumer.CONSUMER_GROUP_ALL))
            consumerGroups.Add(IDynamicLoadConsumer.CONSUMER_GROUP_ALL);

        var dynamicLoadThatCanBeScaledDownOnBehalfOf = Consumers
            .Where(x => x.State == EnergyConsumerState.Running)
            .OfType<IDynamicLoadConsumer>()
            .Where(x => consumerGroups.Contains(x.BalanceOnBehalfOf))
            .Sum(x => x.ReleasablePowerWhenBalancingOnBehalfOf) + dynamicLoadNetChange;

        return Math.Round(dynamicLoadThatCanBeScaledDownOnBehalfOf < 0 ? 0 : dynamicLoadThatCanBeScaledDownOnBehalfOf);
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
        var baseName = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}";

        _logger.LogDebug("{Consumer}: Upserting energy consumer sensors in home assistant.", consumer.Name);

        var stateOptions = new EnumSensorOptions { Icon = "fapro:bolt-auto", Device = GetConsumerDevice(consumer), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
        await _mqttEntityManager.CreateAsync($"{baseName}_state", new EntityCreationOptions(DeviceClass: "enum", UniqueId: $"{baseName}_state", Name: $"Consumer {consumer.Name} - state", Persist: true), stateOptions);

        var startedAtOptions = new EntityOptions { Icon = "mdi:calendar-start-outline", Device = GetConsumerDevice(consumer) };
        await _mqttEntityManager.CreateAsync($"{baseName}_started_at", new EntityCreationOptions(UniqueId: $"{baseName}_started_at", Name: $"Consumer {consumer.Name} - Started at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

        var lastRunOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetConsumerDevice(consumer) };
        await _mqttEntityManager.CreateAsync($"{baseName}_last_run", new EntityCreationOptions(UniqueId: $"{baseName}_last_run", Name: $"Consumer {consumer.Name} - Last run", DeviceClass: "timestamp", Persist: true), lastRunOptions);

    }

    private async Task InitializeDynamicLoadConsumerSensors(EnergyConsumer consumer)
    {
        if (consumer is not IDynamicLoadConsumer dynamicLoadConsumer)
            return;

        var balancingMethodDropdownName = $"select.energy_consumer_{consumer.Name.MakeHaFriendly()}_balancing_method";
        var balanceOnBehalfOfDropdownName = $"select.energy_consumer_{consumer.Name.MakeHaFriendly()}_balance_on_behalf_of";
        var allowBatteryPowerDropDownName = $"select.energy_consumer_{consumer.Name.MakeHaFriendly()}_allow_battery_power";
        var balancingMethodDropdownOptions = new SelectOptions
        {
            Icon = "mdi:car-turbocharger",
            Options = Enum<BalancingMethod>.AllValuesAsStringList(),
            Device = GetConsumerDevice(consumer)
        };

        List<String> consumerGroups = [IDynamicLoadConsumer.CONSUMER_GROUP_SELF];
        consumerGroups.AddRange(Consumers.SelectMany(x => x.ConsumerGroups).Distinct());
        consumerGroups.Add(IDynamicLoadConsumer.CONSUMER_GROUP_ALL);

        var balanceOnBehalfOfDropdownOptions = new SelectOptions
        {
            Icon = "mdi:car-turbocharger", //TODO
            Options = consumerGroups,
            Device = GetConsumerDevice(consumer)
        };

        var allowBatteryPowerDropDownOptions = new SelectOptions
        {
            Icon = "fapro:battery-bolt",
            Options = Enum<AllowBatteryPower>.AllValuesAsStringList(),
            Device = GetConsumerDevice(consumer)
        };

        await _mqttEntityManager.CreateAsync(balancingMethodDropdownName, new EntityCreationOptions(UniqueId: balancingMethodDropdownName, Name: $"Dynamic load balancing method - {consumer.Name}", DeviceClass: "select", Persist: true), balancingMethodDropdownOptions);
        await _mqttEntityManager.SetStateAsync(balancingMethodDropdownName, dynamicLoadConsumer.BalancingMethod.ToString());

        await _mqttEntityManager.CreateAsync(balanceOnBehalfOfDropdownName, new EntityCreationOptions(UniqueId: balanceOnBehalfOfDropdownName, Name: $"Balance on behalf of - {consumer.Name}", DeviceClass: "select", Persist: true), balanceOnBehalfOfDropdownOptions);
        await _mqttEntityManager.SetStateAsync(balanceOnBehalfOfDropdownName, dynamicLoadConsumer.BalanceOnBehalfOf);

        await _mqttEntityManager.CreateAsync(allowBatteryPowerDropDownName, new EntityCreationOptions(UniqueId: allowBatteryPowerDropDownName, Name: $"Allow battery power - {consumer.Name}", DeviceClass: "select", Persist: true), allowBatteryPowerDropDownOptions);
        await _mqttEntityManager.SetStateAsync(allowBatteryPowerDropDownName, dynamicLoadConsumer.AllowBatteryPower.ToString());

        var balancingMethodObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(balancingMethodDropdownName);
        dynamicLoadConsumer.BalancingMethodChangedCommandHandler = balancingMethodObserver.SubscribeAsync(SetBalancingMethodHandler(consumer, dynamicLoadConsumer, balancingMethodDropdownName));

        var balanceOnBehalfOfObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(balanceOnBehalfOfDropdownName);
        dynamicLoadConsumer.BalanceOnBehalfOfChangedCommandHandler = balanceOnBehalfOfObserver.SubscribeAsync(SetBalanceOnBehalfOfHandler(consumer, dynamicLoadConsumer, balanceOnBehalfOfDropdownName));

        var allowBatteryPowerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(allowBatteryPowerDropDownName);
        dynamicLoadConsumer.AllowBatteryPowerChangedCommandHandler = allowBatteryPowerObserver.SubscribeAsync(SetAllowBatteryPower(consumer, dynamicLoadConsumer, allowBatteryPowerDropDownName));

    }

    private Func<string, Task> SetBalancingMethodHandler(EnergyConsumer consumer, IDynamicLoadConsumer dynamicLoadConsumer, string selectName) =>
        async state =>
        {
            _logger.LogDebug("{Consumer}: Setting dynamic load balancing method to {State}.", consumer.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            dynamicLoadConsumer.SetBalancingMethod(_scheduler.Now, Enum<BalancingMethod>.Cast(state));
            await DebounceUpdateInHomeAssistant(consumer);
        };

    private Func<string, Task> SetBalanceOnBehalfOfHandler(EnergyConsumer consumer, IDynamicLoadConsumer dynamicLoadConsumer, string selectName) =>
        async state =>
        {
            _logger.LogDebug("{Consumer}: Setting balance on behalf of to {State}.", consumer.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            dynamicLoadConsumer.SetBalanceOnBehalfOf(state);
            await DebounceUpdateInHomeAssistant(consumer);
        };

    private Func<string, Task> SetAllowBatteryPower(EnergyConsumer consumer, IDynamicLoadConsumer dynamicLoadConsumer, string selectName) =>
        async state =>
        {
            _logger.LogDebug("{Consumer}: Setting allow battery power to {State}.", consumer.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            dynamicLoadConsumer.SetAllowBatteryPower(Enum<AllowBatteryPower>.Cast(state));
            await DebounceUpdateInHomeAssistant(consumer);
        };


    private void InitializeState(EnergyConsumer consumer)
    {
        var storedEnergyConsumerState = _fileStorage.Get<EnergyConsumerFileStorage>("EnergyManager", $"{consumer.Name.MakeHaFriendly()}");

        if (storedEnergyConsumerState == null)
            return;

        consumer.SetState(_scheduler, storedEnergyConsumerState.State, storedEnergyConsumerState.StartedAt, storedEnergyConsumerState.LastRun);

        if (consumer is not IDynamicLoadConsumer dynamicLoadConsumer)
            return;

        dynamicLoadConsumer.SetBalancingMethod(_scheduler.Now, storedEnergyConsumerState.BalancingMethod ?? BalancingMethod.SolarOnly);
        dynamicLoadConsumer.SetBalanceOnBehalfOf(storedEnergyConsumerState.BalanceOnBehalfOf ?? "Self");
        dynamicLoadConsumer.SetAllowBatteryPower(storedEnergyConsumerState.AllowBatteryPower ?? AllowBatteryPower.No);
    }

    public eLime.NetDaemonApps.Domain.Mqtt.Device GetGlobalDevice()
    {
        return new eLime.NetDaemonApps.Domain.Mqtt.Device { Identifiers = [$"energy_manager"], Name = "Energy manager", Manufacturer = "Me" };
    }

    public eLime.NetDaemonApps.Domain.Mqtt.Device GetConsumerDevice(EnergyConsumer consumer)
    {
        return new eLime.NetDaemonApps.Domain.Mqtt.Device { Identifiers = [$"energy_consumer_{consumer.Name.MakeHaFriendly()}"], Name = "Energy consumer: " + consumer.Name, Manufacturer = "Me" };
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
            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", consumer.StartedAt != null ? consumer.StartedAt.Value.ToString("O") : "None");
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_run", consumer.LastRun != null ? consumer.LastRun.Value.ToString("O") : "None");

            _fileStorage.Save("EnergyManager", $"{consumer.Name.MakeHaFriendly()}", consumer.ToFileStorage());

            _logger.LogTrace("{Consumer}: Updated Consumer state sensors in home assistant.", consumer.Name);
        }

        foreach (var battery in Batteries)
        {
            _fileStorage.Save("EnergyManager", $"{battery.Name.MakeHaFriendly()}", battery.ToFileStorage());
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