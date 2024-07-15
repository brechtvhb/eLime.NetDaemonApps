using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FlexiScreen : IDisposable
{
    public string? Name { get; }
    private Boolean IsEnabled { get; set; }
    public Cover Screen { get; }
    private string NetDaemonUserId { get; }

    public SunProtector SunProtector { get; }
    public StormProtector? StormProtector { get; }
    public TemperatureProtector? TemperatureProtector { get; }
    public ManIsAngryProtector? ManIsAngryProtector { get; }
    public WomanIsAngryProtector? WomanIsAngryProtector { get; }
    public FrostProtector FrostProtector { get; }
    public ChildrenAreAngryProtector? ChildrenAreAngryProtector { get; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private FlexiScreenFileStorage? _lastState;

    private DateTimeOffset? LastAutomatedStateChange { get; set; }
    private DateTimeOffset? LastManualStateChange { get; set; }

    private Protectors? LastStateChangeTriggeredBy { get; set; }

    private IDisposable SwitchDisposable { get; set; }

    private IDisposable? GuardTask { get; set; }

    private ScreenState? CurrentState => Screen.IsClosed()
        ? ScreenState.Down
        : Screen.IsOpen()
            ? ScreenState.Up
            : null;

    private readonly DebounceDispatcher? GuardScreenDebounceDispatcher;


    public FlexiScreen(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, Boolean enabled, String name, Cover screen, String ndUserId,
        SunProtector sunProtector, StormProtector? stormProtector, TemperatureProtector? temperatureProtector, ManIsAngryProtector? manIsAngryProtector, WomanIsAngryProtector? womanIsAngryProtector, FrostProtector frostProtector, ChildrenAreAngryProtector? childrenAreAngryProtector,
        TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        if (!enabled)
            return;

        Name = name;
        NetDaemonUserId = ndUserId;

        Screen = screen;
        Screen.StateChanged += Screen_StateChanged;

        SunProtector = sunProtector;
        SunProtector.DesiredStateChanged += Protector_DesiredStateChanged;

        StormProtector = stormProtector;
        if (StormProtector != null)
            StormProtector.DesiredStateChanged += Protector_DesiredStateChanged;

        TemperatureProtector = temperatureProtector;
        if (TemperatureProtector != null)
            TemperatureProtector.DesiredStateChanged += Protector_DesiredStateChanged;

        ManIsAngryProtector = manIsAngryProtector;
        WomanIsAngryProtector = womanIsAngryProtector;
        FrostProtector = frostProtector;
        ChildrenAreAngryProtector = childrenAreAngryProtector;

        if (ChildrenAreAngryProtector != null)
        {
            ChildrenAreAngryProtector.NightStarted += ChildrenAreAngryProtector_NightStarted;
            ChildrenAreAngryProtector.NightEnded += ChildrenAreAngryProtector_NightEnded;
            ChildrenAreAngryProtector.DesiredStateChanged += Protector_DesiredStateChanged;
        }

        EnsureSensorsExist().RunSync();
        InitializeState();

        SunProtector.CheckDesiredState(false);
        stormProtector?.CheckDesiredState(false);
        TemperatureProtector?.CheckDesiredState(false);
        ChildrenAreAngryProtector?.CheckDesiredState(false); //Check after handlers have been assigned and stormy night parameter has been set

        _logger.LogInformation($"{{Screen}}: Desired state for SunProtector is: {SunProtector.DesiredState.State} (enforce: {SunProtector.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for StormProtector is: {StormProtector?.DesiredState.State} (enforce: {StormProtector?.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for TemperatureProtector is: {TemperatureProtector?.DesiredState.State} (enforce: {TemperatureProtector?.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for ChildrenAreAngryProtector is: {ChildrenAreAngryProtector?.DesiredState.State} (enforce: {ChildrenAreAngryProtector?.DesiredState.Enforce}).", Name);


        if (debounceDuration != TimeSpan.Zero)
        {
            GuardScreenDebounceDispatcher = new(debounceDuration);
        }

        GuardScreen().RunSync();

        GuardTask = _scheduler.RunEvery(TimeSpan.FromMinutes(1), _scheduler.Now, () =>
        {
            DebounceGuardScreen().RunSync();
        });
    }

    private void ChildrenAreAngryProtector_NightStarted(object? sender, EventArgs e)
    {
        StormProtector?.CheckForStormyNight();
    }
    private void ChildrenAreAngryProtector_NightEnded(object? sender, EventArgs e)
    {
        StormProtector?.EndNight();
    }

    private async void Protector_DesiredStateChanged(object? sender, DesiredStateEventArgs e)
    {
        _logger.LogInformation($"{{Screen}}: Desired state for {e.Protector} changed to {e.DesiredState} (enforce: {e.Enforce}).", Name);
        await DebounceGuardScreen();
    }

    private async void Screen_StateChanged(object? sender, CoverEventArgs e)
    {
        //NetDaemon user ID is no longer passed along when state transitions from closing to closed or from opening to opened :/
        if (e.New?.Context?.UserId != NetDaemonUserId && e.New?.State is "closing" or "opening")
        {
            LastManualStateChange = _scheduler.Now;
            LastStateChangeTriggeredBy = Protectors.WomanIsAngryProtector;
            _logger.LogInformation($"{{Screen}}: Manual state change detected ({e.New?.State}) UserID was {e.New?.Context?.UserId} (NetDaemonUserID is {NetDaemonUserId}).", Name);
        }

        if (e.Sensor.IsOpen() || e.Sensor.IsClosed())
            await UpdateStateInHomeAssistant();
    }

    internal async Task DebounceGuardScreen()
    {
        if (GuardScreenDebounceDispatcher == null)
        {
            await GuardScreen();
            return;
        }

        await GuardScreenDebounceDispatcher.DebounceAsync(GuardScreen);
    }

    private async Task GuardScreen()
    {
        if (!IsEnabled)
            return;

        //State is transitioning
        if (CurrentState == null)
            return;

        var desiredManIsAngryProtectorState = ManIsAngryProtector?.GetDesiredState(_scheduler.Now, LastAutomatedStateChange);
        if (desiredManIsAngryProtectorState is { Enforce: true })
        {
            await ChangeScreenState(desiredManIsAngryProtectorState.Value.State, Protectors.ManIsAngryProtector);
            return;
        }

        if (StormProtector?.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(StormProtector.DesiredState.State, Protectors.StormProtector);
            return;
        }

        var desiredFrostProtectorState = FrostProtector?.GetDesiredState();
        if (desiredFrostProtectorState is { Enforce: true })
        {
            await ChangeScreenState(desiredFrostProtectorState.Value.State, Protectors.FrostProtector);
            return;
        }

        if (SunProtector.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(SunProtector.DesiredState.State, Protectors.SunProtector);
            return;
        }

        var desiredWomanIsAngryProtectorState = WomanIsAngryProtector?.GetDesiredState(_scheduler.Now, LastManualStateChange);
        if (desiredWomanIsAngryProtectorState is { Enforce: true })
        {
            await ChangeScreenState(desiredWomanIsAngryProtectorState.Value.State, Protectors.WomanIsAngryProtector);
            return;
        }

        if (ChildrenAreAngryProtector?.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(ChildrenAreAngryProtector.DesiredState.State, Protectors.ChildrenAreAngryProtector);
            return;
        }

        switch (SunProtector.DesiredState.State)
        {
            case ScreenState.Up:
                await ChangeScreenState(ScreenState.Up, Protectors.SunProtector);
                break;
            case ScreenState.Down when TemperatureProtector?.DesiredState.State == ScreenState.Down:
                await ChangeScreenState(ScreenState.Down, Protectors.TemperatureProtector);
                break;
            case ScreenState.Down when TemperatureProtector?.DesiredState.State == ScreenState.Up:
                await ChangeScreenState(ScreenState.Up, Protectors.TemperatureProtector);
                break;
            case { } when TemperatureProtector?.DesiredState.State is null:
                await ChangeScreenState(SunProtector.DesiredState.State, Protectors.SunProtector);
                break;
        }
    }

    private async Task ChangeScreenState(ScreenState? desiredState, Protectors triggeredBy)
    {
        switch (desiredState)
        {
            case null:
                break;
            case ScreenState.Up when Screen.IsClosed():
                _logger.LogInformation("{Screen}: Changing screen state to {DesiredState}", Name, desiredState);
                Screen.OpenCover();
                LastAutomatedStateChange = _scheduler.Now;
                LastManualStateChange = null;
                LastStateChangeTriggeredBy = triggeredBy;
                await UpdateStateInHomeAssistant();
                break;
            case ScreenState.Down when Screen.IsOpen():
                _logger.LogInformation("{Screen}: Changing screen state to {DesiredState}", Name, desiredState);
                Screen.CloseCover();
                LastAutomatedStateChange = _scheduler.Now;
                LastManualStateChange = null;
                LastStateChangeTriggeredBy = triggeredBy;
                await UpdateStateInHomeAssistant();
                break;
            default:
                await UpdateStateInHomeAssistant();
                break;
        }
    }

    private async Task EnsureSensorsExist()
    {
        var baseName = $"sensor.flexiscreens_{Name.MakeHaFriendly()}";
        var switchName = $"switch.flexiscreens_{Name.MakeHaFriendly()}";

        if (_haContext.Entity(switchName).State == null)
        {
            _logger.LogDebug("{Screen}: Creating entities in home assistant.", Name);
            var enabledSwitchOptions = new EnabledSwitchAttributes { Icon = "mdi:blinds", Device = GetDevice() };
            _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Flexi screen - {Name}", DeviceClass: "switch", Persist: true), enabledSwitchOptions).RunSync();
            IsEnabled = true;
            _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();

            var lastAutomatedStateChangeOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_automated_state_change", new EntityCreationOptions(UniqueId: $"{baseName}_last_automated_state_change", Name: $"Flexi screen {Name} - Last automated state change", DeviceClass: "timestamp", Persist: true), lastAutomatedStateChangeOptions);

            var lastManualStateChangeOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_manual_state_change", new EntityCreationOptions(UniqueId: $"{baseName}_last_manual_state_change", Name: $"Flexi screen {Name} - Last manual state change", DeviceClass: "timestamp", Persist: true), lastManualStateChangeOptions);

            var lastStateChangeTriggeredBy = new EnumSensorOptions { Icon = "mdi:state-machine", Device = GetDevice(), Options = Enum<Protectors>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_last_state_change_triggered_by", new EntityCreationOptions(UniqueId: $"{baseName}_last_state_change_triggered_by", Name: $"Flexi screen {Name} - Last state change triggered by", Persist: true), lastStateChangeTriggeredBy);

            var stormyNight = new EntityOptions { Icon = "fapro:poo-storm", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"binary_{baseName}_stormy_night", new EntityCreationOptions(UniqueId: $"boolean_{baseName}_stormy_night", Name: $"Flexi screen {Name} - Is stormy night", Persist: true), stormyNight);
        }

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
        SwitchDisposable = observer.SubscribeAsync(EnabledSwitchHandler());
    }

    private Func<string, Task> EnabledSwitchHandler()
    {
        return async state =>
        {
            _logger.LogDebug("{Screen}: Setting flexi screen state to {state}.", Name, state);
            if (state == "OFF")
            {
                _logger.LogDebug("{Screen}: Clearing flexi screen state because it was disabled.", Name);
            }

            IsEnabled = state == "ON";
            await UpdateStateInHomeAssistant();
        };
    }

    public Device GetDevice()
    {
        return new Device { Identifiers = new List<string> { $"flexiscreen.{Name.MakeHaFriendly()}" }, Name = "Flexi screen: " + Name, Manufacturer = "Me" };
    }

    private void InitializeState()
    {
        var storedState = _fileStorage.Get<FlexiScreenFileStorage>("FlexiScreens", Name.MakeHaFriendly());

        if (storedState == null)
            return;

        LastAutomatedStateChange = storedState.LastAutomatedStateChange;
        LastManualStateChange = storedState.LastManualStateChange;
        LastStateChangeTriggeredBy = storedState.LastStateChangeTriggeredBy;
        if (storedState.StormyNight)
            StormProtector?.SetStormyNight();

        IsEnabled = storedState.Enabled;

        _logger.LogDebug("Retrieved flexiscreen state for screen '{screen}'.", Name);
    }

    private async Task UpdateStateInHomeAssistant()
    {
        var fileStorage = ToFileStorage();

        if (fileStorage.Equals(_lastState))
            return;

        var baseName = $"sensor.flexiscreens_{Name.MakeHaFriendly()}";
        var switchName = $"switch.flexiscreens_{Name.MakeHaFriendly()}";

        var attributes = new EnabledSwitchAttributes
        {
            LastUpdated = DateTime.Now.ToString("O")
        };

        await _mqttEntityManager.SetStateAsync(switchName, IsEnabled ? "ON" : "OFF");
        await _mqttEntityManager.SetAttributesAsync(switchName, attributes);
        await _mqttEntityManager.SetStateAsync($"{baseName}_last_state_change_triggered_by", LastStateChangeTriggeredBy?.ToString() ?? "unknown");
        await _mqttEntityManager.SetStateAsync($"{baseName}_last_automated_state_change", LastAutomatedStateChange?.ToString("O") ?? "unknown");
        await _mqttEntityManager.SetStateAsync($"{baseName}_last_manual_state_change", LastManualStateChange?.ToString("O") ?? "unknown");
        await _mqttEntityManager.SetStateAsync($"binary_{baseName}_stormy_night", (StormProtector?.StormyNight ?? false) ? "ON" : "OFF");

        _logger.LogTrace("{Screen}: Updated flexiscreen in Home assistant to {Attributes}", Name, attributes);


        _fileStorage.Save("FlexiScreens", Name.MakeHaFriendly(), fileStorage);
        _lastState = fileStorage;
    }

    internal FlexiScreenFileStorage ToFileStorage() => new()
    {
        Enabled = IsEnabled,
        LastAutomatedStateChange = LastAutomatedStateChange,
        LastManualStateChange = LastManualStateChange,
        LastStateChangeTriggeredBy = LastStateChangeTriggeredBy,
        StormyNight = StormProtector?.StormyNight ?? false
    };

    public void Dispose()
    {
        _logger.LogInformation("{Screen}: Disposing", Name);
        Screen.StateChanged -= Screen_StateChanged;
        SunProtector.DesiredStateChanged -= Protector_DesiredStateChanged;

        if (StormProtector != null)
            StormProtector.DesiredStateChanged -= Protector_DesiredStateChanged;

        if (TemperatureProtector != null)
            TemperatureProtector.DesiredStateChanged -= Protector_DesiredStateChanged;

        if (ChildrenAreAngryProtector != null)
        {
            ChildrenAreAngryProtector.NightStarted -= ChildrenAreAngryProtector_NightStarted;
            ChildrenAreAngryProtector.NightEnded -= ChildrenAreAngryProtector_NightEnded;
            ChildrenAreAngryProtector.DesiredStateChanged -= Protector_DesiredStateChanged;
        }

        SunProtector.Dispose();
        StormProtector?.Dispose();
        TemperatureProtector?.Dispose();
        ChildrenAreAngryProtector?.Dispose();

        SwitchDisposable.Dispose();
        Screen.Dispose();

        GuardTask?.Dispose();

        _logger.LogInformation("{Screen}: Disposed", Name);
    }
}