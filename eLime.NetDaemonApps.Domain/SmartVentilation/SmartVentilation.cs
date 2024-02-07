using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class SmartVentilation
{
    private Boolean IsEnabled { get; set; }
    public Climate Climate { get; }
    private string NetDaemonUserId { get; }

    public StatePingPongGuard StatePingPongGuard { get; }
    public IndoorAirQualityGuard IndoorAirQualityGuard { get; }
    public BathroomAirQualityGuard BathroomAirQualityGuard { get; }
    public MoldGuard MoldGuard { get; }
    public DryAirGuard DryAirGuard { get; }
    public ElectricityBillGuard ElectricityBillGuard { get; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private VentilationFileStorage? _lastState;

    private DateTimeOffset? LastStateChange { get; set; }
    private VentilationGuards? LastStateChangeTriggeredBy { get; set; }

    private IDisposable SwitchDisposable { get; set; }

    private IDisposable? GuardTask { get; set; }

    private VentilationState? CurrentState => Climate.Mode;

    private readonly DebounceDispatcher? GuardScreenDebounceDispatcher;

    public SmartVentilation(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, Boolean enabled, Climate climate, String ndUserId,
        StatePingPongGuard statePingPongGuard, IndoorAirQualityGuard indoorAirQualityGuard, BathroomAirQualityGuard bathroomAirQualityGuard, MoldGuard moldGuard, DryAirGuard dryAirGuard, ElectricityBillGuard electricityBillGuard,
        TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        if (!enabled)
            return;

        NetDaemonUserId = ndUserId;

        Climate = climate;
        Climate.StateChanged += Screen_StateChanged;

        StatePingPongGuard = statePingPongGuard;
        IndoorAirQualityGuard = indoorAirQualityGuard;
        BathroomAirQualityGuard = bathroomAirQualityGuard;
        MoldGuard = moldGuard;
        DryAirGuard = dryAirGuard;
        ElectricityBillGuard = electricityBillGuard;

        EnsureSensorsExist().RunSync();
        InitializeState();

        if (debounceDuration != TimeSpan.Zero)
        {
            GuardScreenDebounceDispatcher = new(debounceDuration);
        }

        GuardClimate().RunSync();

        GuardTask = _scheduler.RunEvery(TimeSpan.FromMinutes(1), _scheduler.Now, () =>
        {
            DebounceGuardClimate().RunSync();
        });
    }

    internal async Task DebounceGuardClimate()
    {
        if (GuardScreenDebounceDispatcher == null)
        {
            await GuardClimate();
            return;
        }

        await GuardScreenDebounceDispatcher.DebounceAsync(GuardClimate);
    }

    private async Task GuardClimate()
    {
        if (!IsEnabled)
            return;

        var desiredStateForStatePingPongGuard = StatePingPongGuard.GetDesiredState(CurrentState, LastStateChange);
        if (desiredStateForStatePingPongGuard is { Enforce: true })
        {
            await ChangeState(desiredStateForStatePingPongGuard.State, VentilationGuards.StatePingPong);
            return;
        }

        var desiredStateForIndoorAirQualityGuard = IndoorAirQualityGuard.GetDesiredState();
        if (desiredStateForIndoorAirQualityGuard is { Enforce: true })
        {
            await ChangeState(desiredStateForIndoorAirQualityGuard.State, VentilationGuards.IndoorAirQuality);
            return;
        }

        var desiredStateForIndoorBathroomAirQualityGuard = BathroomAirQualityGuard.GetDesiredState();
        if (desiredStateForIndoorBathroomAirQualityGuard is { Enforce: true })
        {
            await ChangeState(desiredStateForIndoorBathroomAirQualityGuard.State, VentilationGuards.BathroomAirQuality);
            return;
        }

        if (desiredStateForIndoorAirQualityGuard.State == VentilationState.Medium || desiredStateForIndoorBathroomAirQualityGuard.State == VentilationState.Medium)
        {
            await ChangeState(VentilationState.Medium, VentilationGuards.BathroomAirQuality);
            return;
        }

        var desiredStateForMoldGuard = MoldGuard.GetDesiredState(CurrentState, LastStateChange);
        if (desiredStateForIndoorBathroomAirQualityGuard is { Enforce: true })
        {
            await ChangeState(desiredStateForMoldGuard.State, VentilationGuards.Mold);
            return;
        }

        var desiredStateForDryAirGuard = DryAirGuard.GetDesiredState();
        if (desiredStateForDryAirGuard is { Enforce: true })
        {
            await ChangeState(desiredStateForDryAirGuard.State, VentilationGuards.DryAir);
            return;
        }

        var desiredStateForElectricityBillGuard = ElectricityBillGuard.GetDesiredState();
        await ChangeState(desiredStateForElectricityBillGuard.State, VentilationGuards.ElectricityBill);

    }

    private async void Screen_StateChanged(object? sender, ClimateEventArgs e)
    {
        //NetDaemon user ID is no longer passed along when state transitions from closing to closed or from opening to opened :/
        if (e.New?.Context?.UserId != NetDaemonUserId)
        {
            LastStateChange = _scheduler.Now;
            LastStateChangeTriggeredBy = VentilationGuards.Manual;
            _logger.LogInformation($"Manual state change detected ({e.New?.State}) UserID was {e.New?.Context?.UserId} (NetDaemonUserID is {NetDaemonUserId}).");
        }

        await UpdateStateInHomeAssistant();
    }

    private async Task ChangeState(VentilationState? desiredState, VentilationGuards triggeredBy)
    {
        if (desiredState == null)
            return;

        if (desiredState != Climate.Mode)
        {
            _logger.LogInformation("Changing Ventilation mode to {DesiredState}", desiredState);
            Climate.SetFanMode(desiredState.Value);
            LastStateChange = _scheduler.Now;
            LastStateChangeTriggeredBy = triggeredBy;
            await UpdateStateInHomeAssistant();
        }
    }

    public Device GetDevice()
    {
        return new Device { Identifiers = new List<string> { $"smart_ventilation" }, Name = "Smart ventilation", Manufacturer = "Me" };
    }

    private async Task EnsureSensorsExist()
    {
        var baseName = $"sensor.smart_ventilation";
        var switchName = $"switch.smart_ventilation";

        if (_haContext.Entity(switchName).State == null)
        {
            _logger.LogDebug("Creating entities in home assistant.");
            var enabledSwitchOptions = new EnabledSwitchAttributes { Icon = "fapro:fan", Device = GetDevice() };
            _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Smart ventilation", DeviceClass: "switch", Persist: true), enabledSwitchOptions).RunSync();
            IsEnabled = true;
            _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();

            var lastStateChange = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_state_change", new EntityCreationOptions(UniqueId: $"{baseName}_last_state_change", Name: $"Smart ventilation - Last state change", DeviceClass: "timestamp", Persist: true), lastStateChange);

            var lastStateChangeTriggeredBy = new EnumSensorOptions { Icon = "mdi:state-machine", Device = GetDevice(), Options = Enum<VentilationGuards>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_state_change_triggered_by", new EntityCreationOptions(UniqueId: $"{baseName}_last_state_change_triggered_by", Name: $"Smart ventilation - Last state change triggered by", Persist: true), lastStateChangeTriggeredBy);
        }

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
        SwitchDisposable = observer.SubscribeAsync(EnabledSwitchHandler());
    }

    private Func<string, Task> EnabledSwitchHandler()
    {
        return async state =>
        {
            _logger.LogDebug("Setting smart ventilation state to {state}.", state);
            if (state == "OFF")
            {
                _logger.LogDebug("Clearing smart ventilation state because it was disabled.");
            }

            IsEnabled = state == "ON";
            await UpdateStateInHomeAssistant();
        };
    }

    private void InitializeState()
    {
        var storedState = _fileStorage.Get<VentilationFileStorage>("SmartVentilation", "SmartVentilation");

        if (storedState == null)
            return;

        LastStateChange = storedState.LastStateChange;
        LastStateChangeTriggeredBy = storedState.LastStateChangeTriggeredBy;
        IsEnabled = storedState.Enabled;

        _logger.LogDebug("Retrieved smart ventilation state");
    }

    private async Task UpdateStateInHomeAssistant()
    {
        var fileStorage = ToFileStorage();
        if (fileStorage.Equals(_lastState))
            return;

        var baseName = $"sensor.smart_ventilation";
        var switchName = $"switch.smart_ventilation";

        var attributes = new EnabledSwitchAttributes
        {
            LastUpdated = DateTime.Now.ToString("O")
        };

        await _mqttEntityManager.SetStateAsync(switchName, IsEnabled ? "ON" : "OFF");
        await _mqttEntityManager.SetAttributesAsync(switchName, attributes);
        await _mqttEntityManager.SetStateAsync($"{baseName}_last_state_change_triggered_by", LastStateChangeTriggeredBy?.ToString());
        await _mqttEntityManager.SetStateAsync($"{baseName}_last_state_change", LastStateChange?.ToString("O"));

        _logger.LogTrace("Updated smart ventilation in Home assistant to {Attributes}", attributes);


        _fileStorage.Save("SmartVentilation", "SmartVentilation", fileStorage);
        _lastState = fileStorage;
    }


    internal VentilationFileStorage ToFileStorage() => new()
    {
        Enabled = IsEnabled,
        LastStateChange = LastStateChange,
        LastStateChangeTriggeredBy = LastStateChangeTriggeredBy
    };

    public void Dispose()
    {
        _logger.LogInformation("Disposing");
        Climate.StateChanged -= Screen_StateChanged;

        StatePingPongGuard.Dispose();
        IndoorAirQualityGuard.Dispose();
        BathroomAirQualityGuard.Dispose();
        MoldGuard.Dispose();
        DryAirGuard.Dispose();
        ElectricityBillGuard.Dispose();

        SwitchDisposable.Dispose();
        Climate.Dispose();

        GuardTask?.Dispose();

        _logger.LogInformation("Disposed");
    }
}