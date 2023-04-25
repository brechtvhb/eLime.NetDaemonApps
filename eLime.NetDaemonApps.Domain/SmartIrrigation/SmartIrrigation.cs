﻿using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class SmartIrrigation : IDisposable
{
    public List<ZoneWrapper> Zones { get; }

    public BinarySwitch PumpSocket { get; }
    public Int32 PumpFlowRate { get; }
    public NumericSensor AvailableRainWaterSensor { get; }
    public Int32 MinimumAvailableRainWater { get; }
    public Boolean EnergyAvailable { get; internal set; }

    public NeedsWatering State => Zones.Any(x => x.Zone.State == NeedsWatering.Critical)
        ? NeedsWatering.Critical
        : Zones.Any(x => x.Zone.State == NeedsWatering.Yes)
            ? NeedsWatering.Yes
            : Zones.Any(x => x.Zone.State == NeedsWatering.Ongoing)
                ? NeedsWatering.Ongoing
                : NeedsWatering.No;

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;

    private readonly IMqttEntityManager _mqttEntityManager;

    private IDisposable? EnergyAvailableStateCHangedCommandHandler { get; set; }
    private IDisposable? GuardTask { get; set; }

    public SmartIrrigation(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, BinarySwitch pumpSocket, Int32 pumpFlowRate, NumericSensor availableRainWaterSensor, Int32 minimumAvailableRainWater, List<IrrigationZone> zones, TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;

        PumpSocket = pumpSocket;
        PumpFlowRate = pumpFlowRate;
        AvailableRainWaterSensor = availableRainWaterSensor;
        MinimumAvailableRainWater = minimumAvailableRainWater;
        Zones = zones.Select(x => new ZoneWrapper { Zone = x }).ToList();

        InitializeStateSensor().RunSync();
        InitializeSolarEnergyAvailableSwitch().RunSync();

        foreach (var wrapper in Zones)
        {
            InitializeModeDropdown(wrapper).RunSync();
            InitializeZoneStateSensor(wrapper).RunSync();
            wrapper.Zone.StateChanged += Zone_StateChanged;
            wrapper.Zone.CheckDesiredState();
        }

        if (debounceDuration != TimeSpan.Zero)
        {
            StartWateringDebounceDispatcher = new(debounceDuration);
            StopWateringDebounceDispatcher = new(debounceDuration);
        }

        GuardTask = _scheduler.RunEvery(TimeSpan.FromMinutes(1), _scheduler.Now, () =>
        {
            DebounceStartWatering();
            DebounceStopWatering();
            UpdateStateInHomeAssistant().RunSync();
        });
    }


    private void Zone_StateChanged(object? sender, IrrigationZoneStateChangedEvent e)
    {
        if (e.Zone.Mode == ZoneMode.Off)
        {
            UpdateStateInHomeAssistant().RunSync();
            return;
        }

        var zoneWrapper = Zones.Single(x => x.Zone.Name == e.Zone.Name);

        _logger.LogInformation("{IrrigationZone}: Needs watering changed to: {State}.", e.Zone.Name, e.State);

        switch (e)
        {
            case IrrigationZoneWateringNeededEvent:
                DebounceStartWatering();
                break;
            case IrrigationZoneWateringStartedEvent:
                SetEndWateringTimer(zoneWrapper, _scheduler.Now);

                break;
            case IrrigationZoneEndWateringEvent:
                zoneWrapper.Zone.Valve.TurnOff();
                zoneWrapper.EndWateringtimer?.Dispose();

                DebounceStartWatering();
                break;
            case IrrigationZoneWateringEndedEvent:
                zoneWrapper.Zone.SetLastWateringDate(_scheduler.Now);
                break;
        }

        UpdateStateInHomeAssistant().RunSync();
    }

    private void SetEndWateringTimer(ZoneWrapper zoneWrapper, DateTimeOffset? startTime = null)
    {
        if (startTime != null)
            zoneWrapper.Zone.SetStartWateringDate(startTime.Value);

        if (zoneWrapper.Zone.WateringStartedAt == null)
            return;

        if (zoneWrapper.Zone is not IZoneWithLimitedRuntime limitedRunTimeZone)
            return;

        var timespan = limitedRunTimeZone.GetRunTime(_scheduler.Now);

        switch (timespan)
        {
            case null:
                return;
            case not null when timespan <= TimeSpan.Zero:
                _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone right now.", zoneWrapper.Zone.Name);
                zoneWrapper.Zone.Valve.TurnOff();
                return;
            case not null when timespan > TimeSpan.Zero:
                _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone in '{TimeSpan}'", zoneWrapper.Zone.Name, timespan.ToString());
                zoneWrapper.EndWateringtimer = _scheduler.Schedule(timespan.Value, zoneWrapper.Zone.Valve.TurnOff);
                return;
        }
    }

    private readonly DebounceDispatcher? StartWateringDebounceDispatcher;
    internal void DebounceStartWatering()
    {
        if (StartWateringDebounceDispatcher == null)
        {
            StartWateringZonesIfNeeded();
            return;
        }

        StartWateringDebounceDispatcher.Debounce(StartWateringZonesIfNeeded);
    }

    private void StartWateringZonesIfNeeded()
    {
        if (AvailableRainWaterSensor.State < MinimumAvailableRainWater)
            return;

        var totalFlowRate = Zones.Where(x => x.Zone.CurrentlyWatering).Sum(x => x.Zone.FlowRate);
        var remainingFlowRate = PumpFlowRate - totalFlowRate;

        var criticalZonesThatNeedWatering = Zones.Where(x => x.Zone is { State: NeedsWatering.Critical, CurrentlyWatering: false });

        foreach (var criticalZone in criticalZonesThatNeedWatering)
        {
            var started = StartWateringIfNeeded(criticalZone, remainingFlowRate);
            if (!started)
                continue;

            _logger.LogDebug("{IrrigationZone}: Will start irrigation, zone is in critical need of water.", criticalZone.Zone.Name);
            remainingFlowRate -= criticalZone.Zone.FlowRate;
        }

        var zonesThatNeedWatering = Zones.Where(x => x.Zone is { State: NeedsWatering.Yes, CurrentlyWatering: false });
        foreach (var zone in zonesThatNeedWatering)
        {
            var started = StartWateringIfNeeded(zone, remainingFlowRate);
            if (!started)
                continue;

            _logger.LogDebug("{IrrigationZone}: Will start irrigation.", zone.Zone.Name);
            remainingFlowRate -= zone.Zone.FlowRate;
        }
    }

    private readonly DebounceDispatcher? StopWateringDebounceDispatcher;
    internal void DebounceStopWatering()
    {
        if (StopWateringDebounceDispatcher == null)
        {
            StopWateringZonesIfNeeded();
            return;
        }

        StopWateringDebounceDispatcher.Debounce(StopWateringZonesIfNeeded);
    }

    private void StopWateringZonesIfNeeded()
    {
        if (AvailableRainWaterSensor.State < MinimumAvailableRainWater)
        {
            foreach (var wrapper in Zones)
                wrapper.Zone.Valve.TurnOff();

            _logger.LogInformation("Stopping watering because available rain water ({AvailableRainWater}) went below minimum available rain water needed ({MinimumAvailableRainWater}).", AvailableRainWaterSensor.State, MinimumAvailableRainWater);

            return;
        }

        var zonesThatShouldForceStopped = Zones.Where(x => x.Zone.CheckForForceStop(_scheduler.Now) && x.Zone.CurrentlyWatering);
        foreach (var wrapper in zonesThatShouldForceStopped)
        {
            _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone right now.", wrapper.Zone.Name);
            wrapper.Zone.Valve.TurnOff();
        }

        var zonesThatNoLongerNeedWatering = Zones.Where(x => x.Zone is { State: NeedsWatering.No, CurrentlyWatering: true });
        foreach (var wrapper in zonesThatNoLongerNeedWatering)
        {
            _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone because it no longer needs watering.", wrapper.Zone.Name);
            wrapper.Zone.Valve.TurnOff();
        }

        if (EnergyAvailable)
            return;

        var zonesWorkingOnAvailableEnergy = Zones.Where(x => x.Zone is { Mode: ZoneMode.EnergyManaged, CurrentlyWatering: true });
        foreach (var wrapper in zonesWorkingOnAvailableEnergy)
        {
            _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone because not enough power is available", wrapper.Zone.Name);
            wrapper.Zone.Valve.TurnOff();
        }
    }

    private bool StartWateringIfNeeded(ZoneWrapper wrapper, int remainingFlowRate)
    {
        if (wrapper.Zone.FlowRate > remainingFlowRate)
        {
            return false;
        }

        if (!wrapper.Zone.CanStartWatering(_scheduler.Now, EnergyAvailable))
            return false;


        wrapper.Zone.Valve.TurnOn();
        return true;
    }

    public void Dispose()
    {
        foreach (var wrapper in Zones)
        {
            _logger.LogInformation("Disposing irrigation zone: {IrrigationZone}", wrapper.Zone.Name);
            wrapper.Zone.Dispose();
            wrapper.ModeChangedCommandHandler?.Dispose();
            wrapper.EndWateringtimer?.Dispose();
        }

        PumpSocket.Dispose();
        AvailableRainWaterSensor.Dispose();
        EnergyAvailableStateCHangedCommandHandler?.Dispose();
        GuardTask?.Dispose();
    }

    private async Task InitializeSolarEnergyAvailableSwitch()
    {
        var switchName = $"switch.irrigation_energy_available";
        var state = _haContext.Entity(switchName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating solar energy available switch.");
            var entityOptions = new EntityOptions { Icon = "mdi:solar-power" };
            await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(UniqueId: switchName, Name: $"Irrigation - Energy available", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(switchName, "OFF");
        }
        else
            EnergyAvailable = state == "ON";

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
        EnergyAvailableStateCHangedCommandHandler = observer.SubscribeAsync(SetEnergyAvailableHandler(switchName));
    }
    private Func<string, Task> SetEnergyAvailableHandler(string switchName)
    {
        return async state =>
        {
            _logger.LogDebug("Setting energy available to: {EnergyAvailable}", state);
            await _mqttEntityManager.SetStateAsync(switchName, state);
            EnergyAvailable = state == "ON";

            if (EnergyAvailable)
                DebounceStartWatering();
            else
                DebounceStopWatering();


            UpdateStateInHomeAssistant().RunSync();
        };
    }
    private async Task InitializeStateSensor()
    {
        var stateName = $"sensor.irrigation_state";
        var state = _haContext.Entity(stateName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating Irrigation state sensor in home assistant.");
            var entityOptions = new EntityOptions { Icon = "far:sprinkler" };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(UniqueId: stateName, Name: $"Irrigation state", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, State.ToString());
        }
    }
    private async Task InitializeModeDropdown(ZoneWrapper wrapper)
    {
        var zone = wrapper.Zone;
        var selectName = $"select.irrigation_zone_{zone.Name.MakeHaFriendly()}_mode";
        var state = _haContext.Entity(selectName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("{IrrigationZone}: Creating Zone mode dropdown in home assistant.", zone.Name);
            var selectOptions = new SelectOptions()
            {
                Icon = "fapro:sprinkler",
                Options = Enum<ZoneMode>.AllValuesAsStringList(),
                Device = GetZoneDevice(zone)
            };

            await _mqttEntityManager.CreateAsync(selectName, new EntityCreationOptions(UniqueId: selectName, Name: $"Irrigation zone mode - {zone.Name}", DeviceClass: "select", Persist: true), selectOptions);
            await _mqttEntityManager.SetStateAsync(selectName, ZoneMode.Off.ToString());
            zone.SetMode(ZoneMode.Off);
        }
        else
        {
            _logger.LogDebug("{IrrigationZone}: Initializing zone.", zone.Name);
            zone.SetMode(Enum<ZoneMode>.Cast(state));

            var entity = new Entity<SmartIrrigationZoneAttributes>(_haContext, selectName);

            if (!String.IsNullOrWhiteSpace(entity.Attributes?.LastWatering))
                zone.SetLastWateringDate(DateTime.Parse(entity.Attributes.LastWatering));

            if (!String.IsNullOrWhiteSpace(entity.Attributes?.WateringStartedAt))
                zone.SetStartWateringDate(DateTime.Parse(entity.Attributes.WateringStartedAt));

            SetEndWateringTimer(wrapper);
        }

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(selectName);
        wrapper.ModeChangedCommandHandler = observer.SubscribeAsync(SetZoneModeHandler(zone, selectName));
    }
    private async Task InitializeZoneStateSensor(ZoneWrapper wrapper)
    {
        var zone = wrapper.Zone;
        var stateName = $"sensor.irrigation_zone_{zone.Name.MakeHaFriendly()}_state";
        var state = _haContext.Entity(stateName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("{IrrigationZone}: Creating Zone state sensor in home assistant.", zone.Name);

            var entityOptions = new EntityOptions { Icon = "fapro:sprinkler", Device = GetZoneDevice(zone) };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(UniqueId: stateName, Name: $"Irrigation zone state - {zone.Name}", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, zone.State.ToString());
        }
        else
            zone.SetState(Enum<NeedsWatering>.Cast(state));
    }

    public Device GetZoneDevice(IrrigationZone zone)
    {
        return new Device { Identifiers = new List<string> { $"irrigation_zone.{zone.Name.MakeHaFriendly()}" }, Name = "Irrigation zone: " + zone.Name, Manufacturer = "Me" };
    }

    private Func<string, Task> SetZoneModeHandler(IrrigationZone zone, string selectName)
    {
        return async state =>
        {
            _logger.LogDebug("{IrrigationZone}: Setting irrigation zone mode to {State}.", zone.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            zone.SetMode(Enum<ZoneMode>.Cast(state));
        };
    }

    private async Task UpdateStateInHomeAssistant()
    {
        await _mqttEntityManager.SetStateAsync("sensor.irrigation_state", State.ToString());
        var globalAttributes = new SmartIrrigationStateAttributes()
        {
            LastUpdated = DateTime.Now.ToString("O"),
            WateringOngoingZones = Zones.Where(x => x.Zone.State == NeedsWatering.Ongoing).Select(x => x.Zone.Name).ToList(),
            NeedWaterZones = Zones.Where(x => x.Zone.State == NeedsWatering.Yes).Select(x => x.Zone.Name).ToList(),
            CriticalNeedWaterZones = Zones.Where(x => x.Zone.State == NeedsWatering.Critical).Select(x => x.Zone.Name).ToList()
        };
        await _mqttEntityManager.SetAttributesAsync("sensor.irrigation_state", globalAttributes);

        foreach (var wrapper in Zones)
        {
            var selectName = $"select.irrigation_zone_{wrapper.Zone.Name.MakeHaFriendly()}_mode";
            var stateName = $"sensor.irrigation_zone_{wrapper.Zone.Name.MakeHaFriendly()}_state";

            var attributes = new SmartIrrigationZoneAttributes()
            {
                LastUpdated = DateTime.Now.ToString("O"),
                WateringStartedAt = wrapper.Zone.WateringStartedAt?.ToString("O"),
                LastWatering = wrapper.Zone.LastWatering?.ToString("O"),
                Icon = "far:sprinkler"
            };
            await _mqttEntityManager.SetAttributesAsync(selectName, attributes);
            await _mqttEntityManager.SetStateAsync(stateName, wrapper.Zone.State.ToString());

            _logger.LogTrace("{IrrigationZone}: Update Zone state sensor in home assistant (attributes: {Attributes})", wrapper.Zone.Name, attributes);
        }
    }
}