using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class SmartIrrigation : IDisposable
{
    public List<ZoneWrapper> Zones { get; private set; }

    public BinarySwitch PumpSocket { get; private set; }
    public Int32 PumpFlowRate { get; private set; }
    public NumericSensor AvailableRainWaterSensor { get; private set; }
    public Int32 MinimumAvailableRainWater { get; private set; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;

    private IDisposable SwitchDisposable { get; set; }


    public SmartIrrigation(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, BinarySwitch pumpSocket, Int32 pumpFlowRate, NumericSensor availableRainWaterSensor, Int32 minimumAvailableRainWater, List<IrrigationZone> zones)
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

        foreach (var wrapper in Zones)
        {
            EnsureModeDropdownExists(wrapper).RunSync();
            wrapper.Zone.StateChanged += Zone_StateChanged;
        }
    }

    private void Zone_StateChanged(object? sender, IrrigationZoneStateChangedEvent e)
    {
        if (e.Zone.Mode == ZoneMode.Off)
            return;

        var zoneWrapper = Zones.Single(x => x.Zone.Name == e.Zone.Name);

        //TODO if energymanaged &  not energyavailable return

        _logger.LogInformation($"{{IrrigationZone}}: State changed to {e.State}.", e.Zone.Name);

        switch (e)
        {
            case IrrigationZoneWateringNeededEvent:
                CheckIrrigationZones(e).RunSync();
                break;
            case IrrigationZoneWateringStartedEvent:
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

                CheckIrrigationZones(e).RunSync();
                break;
        }
    }

    private async Task CheckIrrigationZones(IrrigationZoneStateChangedEvent e)
    {
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
            if (!started)
                remainingFlowRate -= zone.Zone.FlowRate;
        }
    }

    private bool StartWateringIfNeeded(ZoneWrapper zone, int remainingFlowRate)
    {
        if (zone.Zone.FlowRate > remainingFlowRate)
            return false;

        if (!zone.Zone.CanStartWatering(_scheduler.Now))
            return false;

        zone.Zone.Valve.TurnOn();
        return true;
    }

    public void Dispose()
    {
        foreach (var wrapper in Zones)
        {
            _logger.LogInformation("Disposing irrigation zone: {IrrigationZone}", wrapper.Zone.Name);
            wrapper.Zone.Dispose();
            wrapper.ModeChangedCommandHandler.Dispose();
        }

        PumpSocket.Dispose();
        AvailableRainWaterSensor.Dispose();
        SwitchDisposable.Dispose();
    }


    private async Task EnsureModeDropdownExists(ZoneWrapper wrapper)
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

            _mqttEntityManager.CreateAsync(selectName, new EntityCreationOptions(UniqueId: selectName, Name: $"Irrigation zone mode - {zone.Name}", DeviceClass: "select", Persist: true), selectOptions).RunSync();
            _mqttEntityManager.SetStateAsync(selectName, ZoneMode.Automatic.ToString()).RunSync();
        }
        else
            zone.SetMode(Enum<ZoneMode>.Cast(state));

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(selectName);
        wrapper.ModeChangedCommandHandler = observer.SubscribeAsync(SwitchZoneModeHandler(zone, selectName));
    }

    public Device GetZoneDevice(IrrigationZone zone)
    {
        return new Device { Identifiers = new List<string> { $"irrigation_zone.{zone.Name.MakeHaFriendly()}" }, Name = "Irrigation zone: " + zone.Name, Manufacturer = "Me" };
    }

    private Func<string, Task> SwitchZoneModeHandler(IrrigationZone zone, string selectName)
    {
        return async state =>
        {
            _logger.LogDebug("{IrrigationZone}: Setting irrigation zone mode to {State}.", zone.Name, state);
            await _mqttEntityManager.SetStateAsync(selectName, state);
            zone.SetMode(Enum<ZoneMode>.Cast(state));
        };
    }

}

public class SelectOptions
{
    [JsonPropertyName("options")]
    public List<String> Options { get; set; }
    [JsonPropertyName("device")]
    public Device Device { get; set; }

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