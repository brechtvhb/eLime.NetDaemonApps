using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class SmartIrrigation : IDisposable
{
    public List<ZoneWrapper> Zones { get; }

    public BinarySwitch PumpSocket { get; }
    public Int32 PumpFlowRate { get; }
    public NumericSensor AvailableRainWaterSensor { get; }
    public Int32 MinimumAvailableRainWater { get; }
    public Boolean EnergyAvailable { get; private set; }

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
        }

        StartWateringDebounceDispatcher = new(debounceDuration);
        StopWateringDebounceDispatcher = new(debounceDuration);

        GuardTask = _scheduler.RunEvery(TimeSpan.FromMinutes(5), _scheduler.Now, () =>
        {
            DebounceStartWatering();
            DebounceStopWatering();
        });
    }


    private void Zone_StateChanged(object? sender, IrrigationZoneStateChangedEvent e)
    {
        if (e.Zone.Mode == ZoneMode.Off)
            return;

        var zoneWrapper = Zones.Single(x => x.Zone.Name == e.Zone.Name);

        _logger.LogInformation($"{{IrrigationZone}}: State changed to {e.State}.", e.Zone.Name);

        switch (e)
        {
            case IrrigationZoneWateringNeededEvent:
                DebounceStartWatering();
                break;
            case IrrigationZoneWateringStartedEvent:
                e.Zone.SetStartWateringDate(_scheduler.Now);
                switch (e.Zone)
                {
                    case AntiFrostMistingIrrigationZone antiFrostMistingIrrigationZone:
                        zoneWrapper.EndWateringtimer = _scheduler.Schedule(antiFrostMistingIrrigationZone.MistingDuration, (_, _) => e.Zone.Valve.TurnOff());
                        break;
                    case ClassicIrrigationZone classicIrrigationZone:
                        {
                            var timespan = classicIrrigationZone.GetRunTime(_scheduler.Now);

                            if (timespan != null)
                                zoneWrapper.EndWateringtimer = _scheduler.Schedule(timespan, (_, _) => e.Zone.Valve.TurnOff());
                            break;
                        }
                }

                break;
            case IrrigationZoneEndWateringEvent:
                e.Zone.Valve.TurnOff();
                zoneWrapper.EndWateringtimer?.Dispose();

                DebounceStartWatering();
                break;
            case IrrigationZoneWateringEndedEvent:
                e.Zone.SetLastWateringDate(_scheduler.Now);
                break;
        }

        UpdateStateInHomeAssistant().RunSync();
    }

    private readonly DebounceDispatcher StartWateringDebounceDispatcher;
    private void DebounceStartWatering()
    {
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
                remainingFlowRate -= criticalZone.Zone.FlowRate;
        }

        var zonesThatNeedWatering = Zones.Where(x => x.Zone is { State: NeedsWatering.Yes, CurrentlyWatering: false });
        foreach (var zone in zonesThatNeedWatering)
        {
            var started = StartWateringIfNeeded(zone, remainingFlowRate);
            if (started)
                remainingFlowRate -= zone.Zone.FlowRate;
        }
    }

    private readonly DebounceDispatcher StopWateringDebounceDispatcher;
    private void DebounceStopWatering()
    {
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
            wrapper.Zone.Valve.TurnOff();

        var zonesThatNoLongerNeedWatering = Zones.Where(x => x.Zone is { State: NeedsWatering.No, CurrentlyWatering: true });
        foreach (var wrapper in zonesThatNoLongerNeedWatering)
            wrapper.Zone.Valve.TurnOff();

        if (EnergyAvailable)
            return;

        var zonesWorkingOnAvailableEnergy = Zones.Where(x => x.Zone is { Mode: ZoneMode.EnergyManaged, CurrentlyWatering: true });
        foreach (var wrapper in zonesWorkingOnAvailableEnergy)
            wrapper.Zone.Valve.TurnOff();
    }

    private bool StartWateringIfNeeded(ZoneWrapper wrapper, int remainingFlowRate)
    {
        if (wrapper.Zone.FlowRate > remainingFlowRate)
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
            await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(UniqueId: switchName, Name: $"Irrigation - Energy available", Persist: true));
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
        };
    }
    private async Task InitializeStateSensor()
    {
        var stateName = $"sensor.irrigation_state";
        var state = _haContext.Entity(stateName).State;

        if (state == null || string.Equals(state, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating Irrigation state sensor in home assistant.");

            await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(UniqueId: stateName, Name: $"Irrigation state", Persist: true));
            await _mqttEntityManager.SetStateAsync(stateName, State.ToString());
        }
    }

    //TODO: sync on init "off" timers with remaining allowed time (or turn of if already past time)
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
                Options = Enum<ZoneMode>.AllValuesAsStringList(),
                Device = GetZoneDevice(zone)
            };

            await _mqttEntityManager.CreateAsync(selectName, new EntityCreationOptions(UniqueId: selectName, Name: $"Irrigation zone mode - {zone.Name}", DeviceClass: "select", Persist: true), selectOptions);
            await _mqttEntityManager.SetStateAsync(selectName, ZoneMode.Off.ToString());
            zone.SetMode(ZoneMode.Off);
        }
        else
        {
            zone.SetMode(Enum<ZoneMode>.Cast(state));
            var attributes = (SmartIrrigationZoneAttributes?)_haContext.Entity(selectName).Attributes;

            if (!string.IsNullOrWhiteSpace(attributes?.WateringStartedAt))
                zone.SetStartWateringDate(DateTime.Parse(attributes.WateringStartedAt));

            if (!string.IsNullOrWhiteSpace(attributes?.LastWatering))
                zone.SetLastWateringDate(DateTime.Parse(attributes.LastWatering));
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

            var entityOptions = new EntityOptions { Device = GetZoneDevice(zone) };

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

            _logger.LogDebug("{IrrigationZone}: Update Zone state sensor in home assistant (attributes: {Attributes})", wrapper.Zone.Name, attributes);
        }
    }
}

public class EntityOptions
{
    [JsonPropertyName("device")]
    public Device Device { get; set; }
}

public class SelectOptions : EntityOptions
{
    [JsonPropertyName("options")]
    public List<String> Options { get; set; }
}

public class Device
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    [JsonPropertyName("manufacturer")]
    public String Manufacturer { get; set; }
    [JsonPropertyName("identifiers")]
    public List<String> Identifiers { get; set; }
}
public enum ZoneMode
{
    Off,
    Automatic,
    EnergyManaged
}