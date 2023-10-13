using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Services;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class SmartIrrigation : IDisposable
{
    public List<ZoneWrapper> Zones { get; }

    public BinarySwitch PumpSocket { get; }
    public Int32 PumpFlowRate { get; }

    public NumericSensor AvailableRainWaterSensor { get; }
    public Int32 MinimumAvailableRainWater { get; }

    public String? PhoneToNotify { get; }
    public Service Services { get; }

    private Weather? Weather { get; }
    public Int32? RainPredictionDays { get; }
    public Double? RainPredictionLiters { get; }

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
    private readonly IFileStorage _fileStorage;
    private IDisposable? EnergyAvailableStateCHangedCommandHandler { get; set; }
    private IDisposable? GuardTask { get; set; }

    public SmartIrrigation(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, BinarySwitch pumpSocket, Int32 pumpFlowRate, NumericSensor availableRainWaterSensor, Int32 minimumAvailableRainWater, Weather? weather, Int32? rainPredictionDays, Double? rainPredictionLiters, String? phoneToNotify, List<IrrigationZone> zones, TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        PumpSocket = pumpSocket;
        PumpFlowRate = pumpFlowRate;
        AvailableRainWaterSensor = availableRainWaterSensor;
        MinimumAvailableRainWater = minimumAvailableRainWater;

        Weather = weather;
        RainPredictionDays = rainPredictionDays;
        RainPredictionLiters = rainPredictionLiters;

        Services = new Service(_haContext);
        PhoneToNotify = phoneToNotify;

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
        var zoneWrapper = Zones.Single(x => x.Zone.Name == e.Zone.Name);

        if (e.Zone.Mode == ZoneMode.Off)
        {
            var canSendNotification = zoneWrapper.Zone.LastNotification == null || zoneWrapper.Zone.LastNotification.Value.AddHours(4) < _scheduler.Now;
            switch (e)
            {
                case IrrigationZoneWateringNeededEvent { State: NeedsWatering.Critical } when !String.IsNullOrWhiteSpace(PhoneToNotify) && canSendNotification:
                    Services.NotifyPhone(PhoneToNotify, $"{e.Zone.Name} is in critical need of water but it is set to manual watering.", null, "Water");
                    e.Zone.NotificationSent(_scheduler.Now);
                    break;
                case IrrigationZoneWateringNeededEvent when !String.IsNullOrWhiteSpace(PhoneToNotify) && canSendNotification:
                    Services.NotifyPhone(PhoneToNotify, $"{e.Zone.Name} needs water but it is set to manual watering.", null, "Water");
                    e.Zone.NotificationSent(_scheduler.Now);
                    break;
                case IrrigationZoneWateringStartedEvent:
                    SetEndWateringTimer(zoneWrapper, _scheduler.Now);
                    break;
                case IrrigationZoneWateringEndedEvent:
                    zoneWrapper.Zone.SetLastWateringDate(_scheduler.Now);
                    break;
            }
            UpdateStateInHomeAssistant().RunSync();
            return;
        }

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

        //Disable automatic turning off of watering for this zone type when mode is off. For other zones automatic turn off is enabled when mode is off because I forget tend to forget that I turned it on manually.
        if (zoneWrapper.Zone is AntiFrostMistingIrrigationZone && zoneWrapper.Zone.Mode == ZoneMode.Off)
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
                _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone in '{TimeSpan}'", zoneWrapper.Zone.Name, timespan.Round().ToString());
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

        double? predictedRain = null;

        if (Weather?.Attributes?.Forecast != null && RainPredictionDays != null)
            predictedRain = Weather.Attributes.Forecast.Take(RainPredictionDays.Value).Sum(x => x.Precipitation);

        if (predictedRain > RainPredictionLiters)
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
            var zonesThatAreWatering = Zones.Where(x => x.Zone.State == NeedsWatering.Ongoing).ToList();
            if (!zonesThatAreWatering.Any())
                return;

            foreach (var wrapper in zonesThatAreWatering)
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

        if (wrapper.Zone.Mode == ZoneMode.Off)
            return false;

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
            var entityOptions = new EntityOptions() { Icon = "mdi:solar-power", Device = GetGlobalDevice() };
            await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(DeviceClass: "switch", UniqueId: switchName, Name: $"Irrigation - Energy available", Persist: true), entityOptions);
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

        if (state == null)
        {
            _logger.LogDebug("Creating Irrigation state sensor in home assistant.");
            var entityOptions = new EnumSensorOptions() { Icon = "far:sprinkler", Device = GetGlobalDevice(), Options = Enum<NeedsWatering>.AllValuesAsStringList() };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(DeviceClass: "enum", UniqueId: stateName, Name: $"Irrigation state", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, State.ToString());
        }
    }
    private async Task InitializeModeDropdown(ZoneWrapper wrapper)
    {
        var zone = wrapper.Zone;
        var selectName = $"select.irrigation_zone_{zone.Name.MakeHaFriendly()}_mode";
        var state = _haContext.Entity(selectName).State;

        if (state == null)
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
            var storedIrrigationZoneState = _fileStorage.Get<IrrigationZoneFileStorage>("SmartIrrigation", $"{zone.Name.MakeHaFriendly()}");

            if (storedIrrigationZoneState == null)
                return;

            zone.SetMode(storedIrrigationZoneState.Mode);
            zone.SetState(storedIrrigationZoneState.State);

            if (storedIrrigationZoneState.LastRun != null)
                zone.SetLastWateringDate(storedIrrigationZoneState.LastRun.Value);

            if (storedIrrigationZoneState.StartedAt != null)
                zone.SetStartWateringDate(storedIrrigationZoneState.StartedAt.Value);

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

        if (state == null)
        {
            _logger.LogDebug("{IrrigationZone}: Creating Zone state sensor in home assistant.", zone.Name);

            var entityOptions = new EntityOptions { Icon = "fapro:sprinkler", Device = GetZoneDevice(zone) };

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(UniqueId: stateName, Name: $"Irrigation zone state - {zone.Name}", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(stateName, zone.State.ToString());
        }
    }

    public Device GetZoneDevice(IrrigationZone zone)
    {
        return new Device { Identifiers = new List<string> { $"irrigation_zone.{zone.Name.MakeHaFriendly()}" }, Name = "Irrigation zone: " + zone.Name, Manufacturer = "Me" };
    }

    public Device GetGlobalDevice()
    {
        return new Device { Identifiers = new List<string> { $"smart_irrigation" }, Name = "Smart Irrigation", Manufacturer = "Me" };
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

            _fileStorage.Save("EnergyManager", $"{wrapper.Zone.Name.MakeHaFriendly()}", wrapper.Zone.ToFileStorage());

            _logger.LogTrace("{IrrigationZone}: Update Zone state sensor in home assistant (attributes: {Attributes})", wrapper.Zone.Name, attributes);
        }
    }
}