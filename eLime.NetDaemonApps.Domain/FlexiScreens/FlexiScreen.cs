using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FlexiScreen : IDisposable
{
    public string? Name { get; }
    private FlexiScreenEnabledSwitch EnabledSwitch { get; set; }
    public Cover Screen { get; }
    private string NetDaemonUserId { get; }

    public SunProtector SunProtector { get; }
    public StormProtector? StormProtector { get; }
    public TemperatureProtector? TemperatureProtector { get; }
    public ManIsAngryProtector? ManIsAngryProtector { get; }
    public WomanIsAngryProtector? WomanIsAngryProtector { get; }
    public ChildrenAreAngryProtector? ChildrenAreAngryProtector { get; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;

    private DateTime? LastAutomatedStateChange { get; set; }
    private DateTime? LastManualStateChange { get; set; }

    private Protectors? LastStateChangeTriggeredBy { get; set; }

    private IDisposable SwitchDisposable { get; set; }

    private ScreenState? CurrentState => Screen.IsClosed()
        ? ScreenState.Down
        : Screen.IsOpen()
            ? ScreenState.Up
            : null;

    public FlexiScreen(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, Boolean enabled, String name, Cover screen, String ndUserId,
        SunProtector sunProtector, StormProtector? stormProtector, TemperatureProtector? temperatureProtector, ManIsAngryProtector? manIsAngryProtector, WomanIsAngryProtector? womanIsAngryProtector, ChildrenAreAngryProtector? childrenAreAngryProtector)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;

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
        ChildrenAreAngryProtector = childrenAreAngryProtector;

        if (ChildrenAreAngryProtector != null)
        {
            ChildrenAreAngryProtector.NightStarted += ChildrenAreAngryProtector_NightStarted;
            ChildrenAreAngryProtector.NightEnded += ChildrenAreAngryProtector_NightEnded;
            ChildrenAreAngryProtector.DesiredStateChanged += Protector_DesiredStateChanged;
        }

        EnsureEnabledSwitchExists().RunSync();
        RetrieveSateFromHomeAssistant().RunSync();

        _logger.LogInformation($"{{Screen}}: Desired state for SunProtector is: {SunProtector.DesiredState.State} (enforce: {SunProtector.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for StormProtector is: {StormProtector?.DesiredState.State} (enforce: {StormProtector?.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for TemperatureProtector is: {TemperatureProtector?.DesiredState.State} (enforce: {TemperatureProtector?.DesiredState.Enforce}).", Name);
        _logger.LogInformation($"{{Screen}}: Desired state for ChildrenAreAngryProtector is: {ChildrenAreAngryProtector?.DesiredState.State} (enforce: {ChildrenAreAngryProtector?.DesiredState.Enforce}).", Name);

        GuardScreen().RunSync();
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
        await GuardScreen();
    }

    private async void Screen_StateChanged(object? sender, CoverEventArgs e)
    {
        //NetDaemon user ID is no longer passed a long when state transitions from closing to closed or from opening to opened :/
        if (e.New?.Context?.UserId != NetDaemonUserId && e.New?.State is "closing" or "opening")
        {
            LastManualStateChange = DateTime.Now;
            LastStateChangeTriggeredBy = Protectors.WomanIsAngryProtector;
            _logger.LogInformation($"{{Screen}}: Manual state change detected ({e.New?.State}) UserID was {e.New?.Context?.UserId} (NetDaemonUserID is {NetDaemonUserId}).", Name);
        }

        if (e.Sensor.IsOpen() || e.Sensor.IsClosed())
            await UpdateStateInHomeAssistant();
    }

    private async Task GuardScreen()
    {
        if (!IsScreenEnabled())
            return;

        //State is transitioning
        if (CurrentState == null)
            return;

        var desiredManIsAngryProtectorState = ManIsAngryProtector?.GetDesiredState(LastAutomatedStateChange);
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

        if (SunProtector.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(SunProtector.DesiredState.State, Protectors.SunProtector);
            return;
        }

        var desiredWomanIsAngryProtectorState = WomanIsAngryProtector?.GetDesiredState(LastManualStateChange);
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
                LastAutomatedStateChange = DateTime.Now;
                LastManualStateChange = null;
                LastStateChangeTriggeredBy = triggeredBy;
                await UpdateStateInHomeAssistant();
                break;
            case ScreenState.Down when Screen.IsOpen():
                _logger.LogInformation("{Screen}: Changing screen state to {DesiredState}", Name, desiredState);
                Screen.CloseCover();
                LastAutomatedStateChange = DateTime.Now;
                LastManualStateChange = null;
                LastStateChangeTriggeredBy = triggeredBy;
                await UpdateStateInHomeAssistant();
                break;
        }
    }

    private async Task EnsureEnabledSwitchExists()
    {
        var switchName = $"switch.flexiscreens_{Name.MakeHaFriendly()}";

        var created = false;
        if (_haContext.Entity(switchName).State == null || string.Equals(_haContext.Entity(switchName).State, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("{Screen}: Creating Enabled switch in home assistant.", Name);
            _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Flexi screen - {Name}", DeviceClass: "switch", Persist: true)).RunSync();
            created = true;
        }

        EnabledSwitch = new FlexiScreenEnabledSwitch(_haContext, switchName);

        if (created)
        {
            _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();
            UpdateStateInHomeAssistant().RunSync();
        }

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
        SwitchDisposable = observer.SubscribeAsync(EnabledSwitchHandler(switchName));
    }

    private Func<string, Task> EnabledSwitchHandler(string switchName)
    {
        return async state =>
        {
            _logger.LogDebug("{Screen}: Setting flexi screen state to {state}.", Name, state);
            if (state == "OFF")
            {
                _logger.LogDebug("{Screen}: Clearing flexi screen state because it was disabled.", Name);
                await UpdateStateInHomeAssistant();
            }
            await _mqttEntityManager.SetStateAsync(switchName, state);
        };
    }

    private Task RetrieveSateFromHomeAssistant()
    {
        LastAutomatedStateChange = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.LastAutomatedStateChange) ? DateTime.Parse(EnabledSwitch.Attributes.LastAutomatedStateChange) : null;
        LastManualStateChange = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.LastManualStateChange) ? DateTime.Parse(EnabledSwitch.Attributes.LastManualStateChange) : null;
        LastStateChangeTriggeredBy = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.LastStateChangeTriggeredBy) ? Enum<Protectors>.Cast(EnabledSwitch.Attributes.LastStateChangeTriggeredBy) : null;
        _logger.LogDebug("Retrieved flexiscreen state from Home assistant for screen '{screen}'.", Name);

        return Task.CompletedTask;
    }

    private async Task UpdateStateInHomeAssistant()
    {
        if (!IsScreenEnabled())
            return;

        var attributes = new FlexiScreenEnabledSwitchAttributes
        {
            LastAutomatedStateChange = LastAutomatedStateChange?.ToString("O"),
            LastManualStateChange = LastManualStateChange?.ToString("O"),
            LastUpdated = DateTime.Now.ToString("O"),
            LastStateChangeTriggeredBy = LastStateChangeTriggeredBy.ToString(),
            Icon = "mdi:blinds"
        };
        await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
        _logger.LogTrace("{Screen}: Updated flexiscreen state in Home assistant to {Attributes}", Name, attributes);
    }

    private bool IsScreenEnabled()
    {
        return EnabledSwitch.IsOn();
    }

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

        _logger.LogInformation("{Screen}: Disposed", Name);
    }
}