using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FlexiScreen
{
    public string? Name { get; }
    private FlexiScreenEnabledSwitch EnabledSwitch { get; set; }
    private Cover Screen { get; }

    private SunProtector SunProtector { get; }
    private StormProtector? StormProtector { get; }
    private TemperatureProtector? TemperatureProtector { get; }
    private ManIsAngryProtector? ManIsAngryProtector { get; }
    private WomanIsAngryProtector? WomanIsAngryProtector { get; }
    private ChildrenAreAngryProtector? ChildrenAreAngryProtector { get; }

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

    public FlexiScreen(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, Boolean enabled, String name, Cover screen,
        SunProtector sunProtector, StormProtector? stormProtector, TemperatureProtector? temperatureProtector, ManIsAngryProtector? manIsAngryProtector, WomanIsAngryProtector? womanIsAngryProtector, ChildrenAreAngryProtector? childrenAreAngryProtector)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;

        if (!enabled)
            return;

        Name = name;
        Screen = screen;
        SunProtector = sunProtector;
        StormProtector = stormProtector;
        TemperatureProtector = temperatureProtector;
        ManIsAngryProtector = manIsAngryProtector;
        WomanIsAngryProtector = womanIsAngryProtector;
        ChildrenAreAngryProtector = childrenAreAngryProtector;

        EnsureEnabledSwitchExists();

        _scheduler.ScheduleCron("*/10 * * * * *", () => GuardScreen().RunSync());

        RetrieveSateFromHomeAssistant().RunSync();
    }

    private async Task GuardScreen()
    {
        //State is transitioning
        if (CurrentState == null)
            return;

        var desiredStormProtectorState = StormProtector?.GetDesiredState(CurrentState.Value);

        if (desiredStormProtectorState is { Enforce: true, State: { } })
        {
            await ChangeScreenState(desiredStormProtectorState.Value.State.Value);
            return;
        }

        var desiredSunProtectorState = SunProtector.GetDesiredState(CurrentState.Value);
        if (desiredSunProtectorState is { Enforce: true, State: { } })
        {
            await ChangeScreenState(desiredSunProtectorState.State.Value);
            return;
        }

        var stateForSunProtector = desiredStormProtectorState?.State;

        var desiredManIsAngryProtectorState = ManIsAngryProtector?.GetDesiredState(CurrentState.Value);
        if (desiredManIsAngryProtectorState is { Enforce: true, State: { } })
        {
            await ChangeScreenState(desiredManIsAngryProtectorState.Value.State.Value);
            return;
        }

        var desiredWomanIsAngryProtectorState = WomanIsAngryProtector?.GetDesiredState(CurrentState.Value);
        if (desiredWomanIsAngryProtectorState is { Enforce: true, State: { } })
        {
            await ChangeScreenState(desiredWomanIsAngryProtectorState.Value.State.Value);
            return;
        }

        var desiredChildrenAreAngryProtectorState = ChildrenAreAngryProtector?.GetDesiredState(CurrentState.Value);
        if (desiredChildrenAreAngryProtectorState is { Enforce: true, State: { } })
        {
            await ChangeScreenState(desiredChildrenAreAngryProtectorState.Value.State.Value);
            return;
        }

        var desiredTemperatureProtectorState = TemperatureProtector?.GetDesiredState(CurrentState.Value);
        var stateForTemperatureProtector = desiredTemperatureProtectorState?.State;

        if (stateForSunProtector == ScreenState.Up)
        {
            await ChangeScreenState(ScreenState.Up);
            return;
        }

        if (stateForSunProtector == ScreenState.Down && stateForTemperatureProtector == ScreenState.Down)
        {
            await ChangeScreenState(ScreenState.Down);
            return;
        }

        if (stateForSunProtector == ScreenState.Down && stateForTemperatureProtector == ScreenState.Up)
        {
            await ChangeScreenState(ScreenState.Up);
            return;
        }
    }

    private async Task ChangeScreenState(ScreenState desiredState)
    {
        if (desiredState == ScreenState.Up && Screen.IsClosed())
        {
            Screen.OpenCover();
            LastAutomatedStateChange = DateTime.Now;
            await UpdateStateInHomeAssistant();
        }

        if (desiredState == ScreenState.Down && Screen.IsClosed())
        {
            Screen.CloseCover();
            LastAutomatedStateChange = DateTime.Now;
            await UpdateStateInHomeAssistant();
        }

    }

    private void EnsureEnabledSwitchExists()
    {
        var switchName = $"switch.flexilights_{Name.MakeHaFriendly()}";

        var created = false;
        if (_haContext.Entity(switchName).State == null || string.Equals(_haContext.Entity(switchName).State, "unavailable", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogDebug("Creating Enabled switch for room '{room}' in home assistant.", Name);
            _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Flexi lights - {Name}", DeviceClass: "switch", Persist: true)).RunSync();
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
        if (!IsRoomEnabled())
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

    private bool IsRoomEnabled()
    {
        return EnabledSwitch.IsOn();
    }
}