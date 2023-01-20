using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FlexiScreen
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
        Screen.StateChanged += async (o, e) => await Screen_StateChanged(o, e);

        SunProtector = sunProtector;
        SunProtector.DesiredStateChanged += async (_, _) => await GuardScreen();

        StormProtector = stormProtector;
        if (StormProtector != null)
            StormProtector.DesiredStateChanged += async (_, _) => await GuardScreen();

        TemperatureProtector = temperatureProtector;
        if (TemperatureProtector != null)
            TemperatureProtector.DesiredStateChanged += async (_, _) => await GuardScreen();

        ManIsAngryProtector = manIsAngryProtector;
        WomanIsAngryProtector = womanIsAngryProtector;
        ChildrenAreAngryProtector = childrenAreAngryProtector;

        if (ChildrenAreAngryProtector != null)
            ChildrenAreAngryProtector.DesiredStateChanged += async (_, _) => await GuardScreen();

        EnsureEnabledSwitchExists();
        RetrieveSateFromHomeAssistant().RunSync();

        GuardScreen().RunSync();
    }

    private async Task Screen_StateChanged(object? sender, CoverEventArgs e)
    {
        if (e.New?.Context?.UserId != NetDaemonUserId)
            LastManualStateChange = DateTime.Now;

        if (e.Sensor.IsOpen() || e.Sensor.IsClosed())
            await UpdateStateInHomeAssistant();
    }

    private async Task GuardScreen()
    {
        //State is transitioning
        if (CurrentState == null)
            return;

        var desiredManIsAngryProtectorState = ManIsAngryProtector?.GetDesiredState(LastAutomatedStateChange);
        if (desiredManIsAngryProtectorState is { Enforce: true })
        {
            await ChangeScreenState(desiredManIsAngryProtectorState.Value.State);
            return;
        }

        if (StormProtector?.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(StormProtector.DesiredState.State);
            return;
        }

        if (SunProtector.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(SunProtector.DesiredState.State);
            return;
        }

        var desiredWomanIsAngryProtectorState = WomanIsAngryProtector?.GetDesiredState(LastManualStateChange);
        if (desiredWomanIsAngryProtectorState is { Enforce: true })
        {
            await ChangeScreenState(desiredWomanIsAngryProtectorState.Value.State);
            return;
        }

        if (ChildrenAreAngryProtector?.DesiredState is { Enforce: true })
        {
            await ChangeScreenState(ChildrenAreAngryProtector.DesiredState.State);
            return;
        }

        switch (SunProtector.DesiredState.State)
        {
            case ScreenState.Up:
                await ChangeScreenState(ScreenState.Up);
                break;
            case ScreenState.Down when TemperatureProtector?.DesiredState.State == ScreenState.Down:
                await ChangeScreenState(ScreenState.Down);
                break;
            case ScreenState.Down when TemperatureProtector?.DesiredState.State == ScreenState.Up:
                await ChangeScreenState(ScreenState.Up);
                break;
            case { } when TemperatureProtector?.DesiredState.State is null:
                await ChangeScreenState(SunProtector.DesiredState.State);
                break;
        }
    }

    private async Task ChangeScreenState(ScreenState? desiredState)
    {
        switch (desiredState)
        {
            case null:
                break;
            case ScreenState.Up when Screen.IsClosed():
                _logger.LogInformation("Changing screen state for screen {screen} to {desiredState}", Name, desiredState);
                Screen.OpenCover();
                LastAutomatedStateChange = DateTime.Now;
                LastManualStateChange = null;
                await UpdateStateInHomeAssistant();
                break;
            case ScreenState.Down when Screen.IsOpen():
                _logger.LogInformation("Changing screen state for screen {screen} to {desiredState}", Name, desiredState);
                Screen.CloseCover();
                LastAutomatedStateChange = DateTime.Now;
                LastManualStateChange = null;
                await UpdateStateInHomeAssistant();
                break;
        }
    }

    private void EnsureEnabledSwitchExists()
    {
        var switchName = $"switch.flexiscreens_{Name.MakeHaFriendly()}";

        var created = false;
        if (_haContext.Entity(switchName).State == null || string.Equals(_haContext.Entity(switchName).State, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating Enabled switch for screen '{screen}' in home assistant.", Name);
            _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Flexi screen - {Name}", DeviceClass: "switch", Persist: true)).RunSync();
            created = true;
        }

        EnabledSwitch = new FlexiScreenEnabledSwitch(_haContext, switchName);

        if (created)
            _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();

        _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName)
            .RunSync()
            .SubscribeAsync(async state =>
            {
                _logger.LogDebug("Setting flexi screen state for screen '{screen}' to {state}.", Name, state);
                if (state == "OFF")
                {
                    _logger.LogDebug("Clearing flexi screen state because it was disabled for screen '{screen}'.", Name);
                    await UpdateStateInHomeAssistant();
                }
                await _mqttEntityManager.SetStateAsync(switchName, state);
            });

        UpdateStateInHomeAssistant().RunSync();
    }

    private Task RetrieveSateFromHomeAssistant()
    {
        LastAutomatedStateChange = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.LastAutomatedStateChange) ? DateTime.Parse(EnabledSwitch.Attributes.LastAutomatedStateChange) : null;
        LastManualStateChange = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.LastManualStateChange) ? DateTime.Parse(EnabledSwitch.Attributes.LastManualStateChange) : null;

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
            Icon = "mdi:blinds"
        };
        await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
        _logger.LogTrace("Updated flexiscreen state for screen '{screen}' in Home assistant to {attr}", Name, attributes);
    }

    private bool IsScreenEnabled()
    {
        return EnabledSwitch.IsOn();
    }
}