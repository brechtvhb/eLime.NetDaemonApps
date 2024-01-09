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
    public List<IrrigationZone> Zones { get; }

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

    public NeedsWatering State => Zones.Any(x => x.State == NeedsWatering.Critical)
        ? NeedsWatering.Critical
        : Zones.Any(x => x.State == NeedsWatering.Yes)
            ? NeedsWatering.Yes
            : Zones.Any(x => x.State == NeedsWatering.Ongoing)
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

        Zones = zones;

        InitializeStateSensor().RunSync();
        InitializeSolarEnergyAvailableSwitch().RunSync();
        InitializeState();

        foreach (var zone in Zones)
        {
            InitializeModeDropdown(zone).RunSync();
            InitializeZoneSensors(zone).RunSync();
            InitializeState(zone);
            zone.StateChanged += Zone_StateChanged;
            zone.CheckDesiredState();
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
        });
    }


    private void Zone_StateChanged(object? sender, IrrigationZoneStateChangedEvent e)
    {
        var zone = Zones.Single(x => x.Name == e.Zone.Name);
        _logger.LogInformation("{IrrigationZone}: Needs watering changed to: {State}.", e.Zone.Name, e.State);

        if (e.Zone.Mode == ZoneMode.Manual)
        {
            var canSendNotification = zone.LastNotification == null || zone.LastNotification.Value.AddHours(4) < _scheduler.Now;
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
                    zone.Started(_logger, _scheduler, _scheduler.Now);
                    break;
                case IrrigationZoneWateringEndedEvent:
                    zone.SetLastWateringDate(_scheduler.Now);
                    break;
            }
            UpdateStateInHomeAssistant(zone).RunSync();
            return;
        }

        switch (e)
        {
            case IrrigationZoneWateringNeededEvent:
                DebounceStartWatering();
                break;
            case IrrigationZoneWateringStartedEvent:
                zone.Started(_logger, _scheduler, _scheduler.Now);
                break;
            case IrrigationZoneEndWateringEvent:
                zone.Stop();
                DebounceStartWatering();
                break;
            case IrrigationZoneWateringEndedEvent:
                zone.SetLastWateringDate(_scheduler.Now);
                break;
        }

        UpdateStateInHomeAssistant(zone).RunSync();
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

        var totalFlowRate = Zones.Where(x => x.CurrentlyWatering).Sum(x => x.FlowRate);
        var remainingFlowRate = PumpFlowRate - totalFlowRate;

        var criticalZonesThatNeedWatering = Zones.Where(x => x is { State: NeedsWatering.Critical, CurrentlyWatering: false });

        foreach (var criticalZone in criticalZonesThatNeedWatering)
        {
            var started = StartWateringIfNeeded(criticalZone, remainingFlowRate);
            if (!started)
                continue;

            _logger.LogDebug("{IrrigationZone}: Will start irrigation, zone is in critical need of water.", criticalZone.Name);
            remainingFlowRate -= criticalZone.FlowRate;
        }

        var zonesThatNeedWatering = Zones.Where(x => x is { State: NeedsWatering.Yes, CurrentlyWatering: false });
        foreach (var zone in zonesThatNeedWatering)
        {
            var started = StartWateringIfNeeded(zone, remainingFlowRate);
            if (!started)
                continue;

            _logger.LogDebug("{IrrigationZone}: Will start irrigation.", zone.Name);
            remainingFlowRate -= zone.FlowRate;
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
            var zonesThatAreWatering = Zones.Where(x => x.State == NeedsWatering.Ongoing).ToList();
            if (!zonesThatAreWatering.Any())
                return;

            foreach (var zone in zonesThatAreWatering)
                zone.Stop();

            _logger.LogInformation("Stopping watering because available rain water ({AvailableRainWater}) went below minimum available rain water needed ({MinimumAvailableRainWater}).", AvailableRainWaterSensor.State, MinimumAvailableRainWater);

            return;
        }

        var zonesThatShouldForceStopped = Zones.Where(x => x.CheckForForceStop(_scheduler.Now) && x.CurrentlyWatering);
        foreach (var zone in zonesThatShouldForceStopped)
        {
            _logger.LogDebug("{IrrigationZone}: Will force stop irrigation for this zone right now.", zone.Name);
            zone.Stop();
        }

        var zonesThatNoLongerNeedWatering = Zones.Where(x => x is { State: NeedsWatering.No, CurrentlyWatering: true });
        foreach (var zone in zonesThatNoLongerNeedWatering)
        {
            _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone because it no longer needs watering.", zone.Name);
            zone.Stop();
        }

        if (EnergyAvailable)
            return;

        var zonesWorkingOnAvailableEnergy = Zones.Where(x => x is { Mode: ZoneMode.EnergyManaged, CurrentlyWatering: true });
        foreach (var zone in zonesWorkingOnAvailableEnergy)
        {
            _logger.LogDebug("{IrrigationZone}: Will stop irrigation for this zone because not enough power is available", zone.Name);
            zone.Stop();
        }
    }

    private bool StartWateringIfNeeded(IrrigationZone zone, int remainingFlowRate)
    {
        if (zone.FlowRate > remainingFlowRate)
        {
            return false;
        }

        if (zone.Mode == ZoneMode.Manual)
            return false;

        if (!zone.CanStartWatering(_scheduler.Now, EnergyAvailable))
            return false;

        zone.Start();
        return true;
    }

    public void Dispose()
    {
        foreach (var zone in Zones)
        {
            _logger.LogInformation("Disposing irrigation zone: {IrrigationZone}", zone.Name);
            zone.Dispose();
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

        if (state == null)
        {
            _logger.LogDebug("Creating solar energy available switch.");
            var entityOptions = new EntityOptions { Icon = "mdi:solar-power", Device = GetGlobalDevice() };

            await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(DeviceClass: "switch", UniqueId: switchName, Name: $"Irrigation - Energy available", Persist: true), entityOptions);
            await _mqttEntityManager.SetStateAsync(switchName, "OFF");
            EnergyAvailable = false;
        }

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
    private async Task InitializeModeDropdown(IrrigationZone zone)
    {
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
            await _mqttEntityManager.SetStateAsync(selectName, ZoneMode.Manual.ToString());
            zone.SetMode(ZoneMode.Manual);
        }

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(selectName);
        zone.ModeChangedCommandHandler = observer.SubscribeAsync(SetZoneModeHandler(zone, selectName));
    }
    private async Task InitializeZoneSensors(IrrigationZone zone)
    {
        var baseName = $"sensor.irrigation_zone_{zone.Name.MakeHaFriendly()}";
        var state = _haContext.Entity($"{baseName}_state").State;

        if (state == null)
        {
            _logger.LogDebug("{IrrigationZone}: Creating Zone sensors in home assistant.", zone.Name);

            var stateOptions = new EnumSensorOptions { Icon = "fapro:sprinkler", Device = GetZoneDevice(zone), Options = Enum<NeedsWatering>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_state", new EntityCreationOptions(UniqueId: $"{baseName}_state", Name: $"Irrigation zone {zone.Name} - State", Persist: true), stateOptions);

            var startedAtOptions = new EntityOptions { Icon = "mdi:calendar-start-outline", Device = GetZoneDevice(zone) };
            await _mqttEntityManager.CreateAsync($"{baseName}_started_at", new EntityCreationOptions(UniqueId: $"{baseName}_started_at", Name: $"Irrigation zone {zone.Name} - Started at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

            var lastWateringOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetZoneDevice(zone) };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_watering", new EntityCreationOptions(UniqueId: $"{baseName}_last_watering", Name: $"Irrigation zone {zone.Name} - Last watering", DeviceClass: "timestamp", Persist: true), lastWateringOptions);
        }
    }

    private void InitializeState()
    {
        _logger.LogDebug("Initializing global properties.");
        var storedSmartIrrigationSettings = _fileStorage.Get<SmartIrrigationFileStorage>("SmartIrrigation", "Global");

        if (storedSmartIrrigationSettings == null)
            return;

        EnergyAvailable = storedSmartIrrigationSettings.EnergyAvailable;
    }

    private void InitializeState(IrrigationZone zone)
    {
        _logger.LogDebug($"{{IrrigationZone}}: Initializing zone. Season start: {zone.IrrigationSeasonStart}. Season end: {zone.IrrigationSeasonEnd}", zone.Name);
        var storedIrrigationZoneState = _fileStorage.Get<IrrigationZoneFileStorage>("SmartIrrigation", $"{zone.Name.MakeHaFriendly()}");

        if (storedIrrigationZoneState == null)
            return;

        zone.SetMode(storedIrrigationZoneState.Mode);
        zone.SetState(storedIrrigationZoneState.State, storedIrrigationZoneState.StartedAt, storedIrrigationZoneState.LastRun);

        zone.Started(_logger, _scheduler);
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

    private async Task UpdateStateInHomeAssistant(IrrigationZone? changedZone = null)
    {
        await _mqttEntityManager.SetStateAsync("sensor.irrigation_state", State.ToString());
        var globalAttributes = new SmartIrrigationStateAttributes()
        {
            LastUpdated = DateTime.Now.ToString("O"),
            WateringOngoingZones = Zones.Where(x => x.State == NeedsWatering.Ongoing).Select(x => x.Name).ToList(),
            NeedWaterZones = Zones.Where(x => x.State == NeedsWatering.Yes).Select(x => x.Name).ToList(),
            CriticalNeedWaterZones = Zones.Where(x => x.State == NeedsWatering.Critical).Select(x => x.Name).ToList()
        };
        await _mqttEntityManager.SetAttributesAsync("sensor.irrigation_state", globalAttributes);

        _fileStorage.Save("SmartIrrigation", "Global", new SmartIrrigationFileStorage { EnergyAvailable = EnergyAvailable });

        foreach (var zone in Zones)
        {
            if (changedZone != null && changedZone.Name != zone.Name)
                continue;

            var baseName = $"sensor.irrigation_zone_{zone.Name.MakeHaFriendly()}";

            await _mqttEntityManager.SetStateAsync($"{baseName}_state", zone.State.ToString());
            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", zone.WateringStartedAt?.ToString("O")!);
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_watering", zone.LastWatering?.ToString("O")!);

            _fileStorage.Save("SmartIrrigation", $"{zone.Name.MakeHaFriendly()}", zone.ToFileStorage());

            _logger.LogTrace("{IrrigationZone}: Updated sensors in home assistant.", zone.Name);
        }
    }
}