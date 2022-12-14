using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Conditions;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.NumericSensors;
using eLime.NetDaemonApps.Domain.Rooms.Actions;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Action = eLime.NetDaemonApps.Domain.Rooms.Actions.Action;

namespace eLime.NetDaemonApps.Domain.Rooms;

public class Room
{
    public string? Name { get; }
    private EnabledSwitch EnabledSwitch { get; set; }

    public bool AutoTransition { get; }
    public bool AutoTransitionTurnOffIfNoValidSceneFound { get; }
    private bool FullyAutomated { get; }
    private readonly DebounceDispatcher AutoTransitionDebounceDispatcher;

    public InitialClickAfterMotionBehaviour InitialClickAfterMotionBehaviour { get; }
    public Int32? IlluminanceThreshold { get; }
    public bool AutoSwitchOffAboveIlluminance { get; }
    public TimeSpan IgnorePresenceAfterOffDuration { get; }
    public DateTime? TurnOffAt { get; private set; }
    private IDisposable? TurnOffSchedule { get; set; }
    private DateTime? IgnorePresenceUntil { get; set; }
    private IDisposable? ClearIgnorePresenceSchedule { get; set; }
    public InitiatedBy InitiatedBy { get; private set; } = InitiatedBy.NoOne;

    private readonly List<BinarySensor> _offSensors = new();
    public IReadOnlyCollection<BinarySensor> OffSensors => _offSensors.AsReadOnly();

    public void AddOffSensor(BinarySensor sensor)
    {
        sensor.TurnedOn += async (s, e) => await OffSensor_TurnedOn(s, e);
        _offSensors.Add(sensor);
    }

    private readonly List<IlluminanceSensor> _illuminanceSensors = new();
    public IReadOnlyCollection<IlluminanceSensor> IlluminanceSensors => _illuminanceSensors.AsReadOnly();

    public void AddIlluminanceSensor(IlluminanceSensor sensor)
    {
        sensor.WentAboveThreshold += async (s, e) => await Sensor_WentAboveThreshold(s, e);
        _illuminanceSensors.Add(sensor);
    }

    private readonly List<MotionSensor> _motionSensors = new();
    public IReadOnlyCollection<MotionSensor> MotionSensors => _motionSensors.AsReadOnly();

    public void AddMotionSensor(MotionSensor sensor)
    {
        sensor.TurnedOn += async (s, e) => await MotionSensor_TurnedOn(s, e);
        sensor.TurnedOff += async (s, e) => await MotionSensor_TurnedOff(s, e);
        _motionSensors.Add(sensor);
    }

    private readonly List<ISwitch> _switches = new();
    public IReadOnlyCollection<ISwitch> Switches => _switches.AsReadOnly();

    public void AddSwitches(List<ISwitch> switches)
    {
        foreach (var sensor in switches)
        {
            sensor.Clicked += async (s, e) => await Switch_Clicked(s, e);
            sensor.DoubleClicked += async (s, e) => await Switch_DoubleClicked(s, e);
            sensor.TripleClicked += async (s, e) => await Switch_TripleClicked(s, e);
            sensor.LongClicked += async (s, e) => await Switch_LongClicked(s, e);
            sensor.UberLongClicked += async (s, e) => await Switch_UberLongClicked(s, e);
            _switches.Add(sensor);
        }
    }


    private readonly List<Action> _offActions = new();
    public IReadOnlyCollection<Action> OffActions => _offActions.AsReadOnly();
    public void AddOffActions(List<Action> action) => _offActions.AddRange(action);


    private readonly List<Action> _doubleClickActions = new();
    public IReadOnlyCollection<Action> DoubleClickActions => _doubleClickActions.AsReadOnly();
    public void AddDoubleClickActions(List<Action> action) => _doubleClickActions.AddRange(action);


    private readonly List<Action> _tripleClickActions = new();
    public IReadOnlyCollection<Action> TripleClickActions => _tripleClickActions.AsReadOnly();
    public void AddTripleClickActions(List<Action> action) => _tripleClickActions.AddRange(action);


    private readonly List<Action> _longClickActions = new();
    public IReadOnlyCollection<Action> LongClickActions => _longClickActions.AsReadOnly();
    public void AddLongClickActions(List<Action> action) => _longClickActions.AddRange(action);


    private readonly List<Action> _uberLongClickActions = new();
    public IReadOnlyCollection<Action> UberLongClickActions => _uberLongClickActions.AsReadOnly();
    public void AddUberLongClickActions(List<Action> action) => _uberLongClickActions.AddRange(action);


    private readonly List<Entity> _flexiSceneSensors = new();
    public IReadOnlyCollection<Entity> FlexiSceneSensors => _flexiSceneSensors.AsReadOnly();

    public void AddFlexiSceneSensor(Entity sensor)
    {
        if (sensor is BinarySensor binarySensor)
        {
            binarySensor.TurnedOn += async (_, _) => await AutoTransitionDebounceDispatcher.DebounceAsync(ExecuteFlexiSceneOnAutoTransition);
            binarySensor.TurnedOff += async (_, _) => await AutoTransitionDebounceDispatcher.DebounceAsync(ExecuteFlexiSceneOnAutoTransition);
            _flexiSceneSensors.Add(binarySensor);
        }
    }

    public FlexiScenes FlexiScenes { get; }
    public FlexiScene? FlexiSceneThatShouldActivate => FlexiScenes.GetSceneThatShouldActivate(FlexiSceneSensors);


    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;


    public Room(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, RoomConfig config, TimeSpan autoTransitionDebounce)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;

        Name = config.Name;
        EnsureEnabledSwitchExists();

        AutoTransitionDebounceDispatcher = new(autoTransitionDebounce);
        if (!(config.Enabled ?? true))
        {
            FlexiScenes = new FlexiScenes(new List<FlexiScene>());
            return;
        }

        AutoTransition = config.AutoTransition;
        AutoTransitionTurnOffIfNoValidSceneFound = config.AutoTransitionTurnOffIfNoValidSceneFound;
        InitialClickAfterMotionBehaviour = config.InitialClickAfterMotionBehaviour == Config.FlexiLights.InitialClickAfterMotionBehaviour.ChangeOffDurationOnly ? InitialClickAfterMotionBehaviour.ChangeOffDurationOnly : InitialClickAfterMotionBehaviour.ChangeOFfDurationAndGoToNextFlexiScene;
        IlluminanceThreshold = config.IlluminanceThreshold;
        AutoSwitchOffAboveIlluminance = config.AutoSwitchOffAboveIlluminance;
        IgnorePresenceAfterOffDuration = config.IgnorePresenceAfterOffDuration ?? TimeSpan.Zero;

        if (config.OffSensors != null && config.OffSensors.Any())
        {
            foreach (var sensorId in config.OffSensors)
            {
                if (OffSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = BinarySensor.Create(_haContext, sensorId);
                AddOffSensor(sensor);
            }
        }

        if (config.IlluminanceSensors != null && config.IlluminanceSensors.Any())
        {
            foreach (var sensorId in config.IlluminanceSensors)
            {
                if (IlluminanceSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = IlluminanceSensor.Create(_haContext, sensorId, config.IlluminanceThreshold);
                AddIlluminanceSensor(sensor);
            }
        }

        if (config.MotionSensors != null && config.MotionSensors.Any())
        {
            foreach (var sensorId in config.MotionSensors)
            {
                if (MotionSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = MotionSensor.Create(_haContext, sensorId);
                AddMotionSensor(sensor);
            }
        }

        var switches = config.Switches.ConvertToDomainModel(config.ClickInterval, config.LongClickDuration, config.UberLongClickDuration,
            config.SinglePressState, config.DoublePressState, config.TriplePressState, config.LongPressState, config.UberLongPressState, _haContext);

        AddSwitches(switches);

        if (config.OffActions == null || !config.OffActions.Any())
            throw new Exception("Define at least one off action");

        if (config.OffActions.Any(x => x.ExecuteOffActions))
            throw new ArgumentException("Do not define ExecuteOFfActions within OFF actions. That would cause a circular dependency.");

        var offActions = config.OffActions.ConvertToDomainModel(_haContext);
        AddOffActions(offActions);

        var doubleClickActions = config.DoubleClickActions.ConvertToDomainModel(_haContext);
        AddDoubleClickActions(doubleClickActions);

        var tripleClickActions = config.TripleClickActions.ConvertToDomainModel(_haContext);
        AddTripleClickActions(tripleClickActions);

        var longClickActions = config.LongClickActions.ConvertToDomainModel(_haContext);
        AddLongClickActions(longClickActions);

        var uberLongClickActions = config.UberLongClickActions.ConvertToDomainModel(_haContext);
        AddUberLongClickActions(uberLongClickActions);

        var flexiScenes = new List<FlexiScene>();
        foreach (var flexiSceneConfig in config.FlexiScenes)
        {
            var flexiScene = FlexiScene.Create(_haContext, flexiSceneConfig);

            if (!flexiScene.Conditions.Any())
            {
                logger.LogTrace($"No conditions were found on flexi scene '{flexiScene.Name}'. This will always resolve to true. Intended or configuration problem?");
            }

            flexiScenes.Add(flexiScene);

            foreach (var flexiSceneSensorId in flexiScene.GetSensorsIds())
            {
                if (FlexiSceneSensors.Any(x => x.EntityId == flexiSceneSensorId.Item1))
                    continue;

                if (flexiSceneSensorId.Item2 == ConditionSensorType.Binary)
                {
                    var binarySensor = BinarySensor.Create(_haContext, flexiSceneSensorId.Item1);
                    AddFlexiSceneSensor(binarySensor);
                }
            }

        }

        FlexiScenes = new FlexiScenes(flexiScenes);

        if (!Switches.Any() && !MotionSensors.Any() && AutoTransition == false)
            throw new Exception("Define at least one switch or motion sensor or run in full automation mode (auto transition true and at least one flexiscene with conditions)");

        if (!Switches.Any() && !MotionSensors.Any() && AutoTransition)
            FullyAutomated = true;

        if (FullyAutomated && FlexiScenes.All.All(x => !x.Conditions.Any()))
            throw new Exception("Define at least one flexi scene with conditions when running in full automation mode (no switches & motion sensors defined & auto transition: true)");

        RetrieveSateFromHomeAssistant().RunSync();

        _logger.LogInformation("{Room}: Initialized with scenes: {Scenes}.", Name, String.Join(", ", FlexiScenes.All.Select(x => x.Name)));

        if (FullyAutomated)
            ExecuteFlexiSceneOnAutoTransition().RunSync();
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

        EnabledSwitch = new EnabledSwitch(_haContext, switchName);

        if (created)
            _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();

        _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName)
            .RunSync()
            .SubscribeAsync(async state =>
            {
                _logger.LogDebug("Setting flexi lights state for room '{room}' to {state}.", Name, state);
                if (state == "OFF")
                {
                    _logger.LogDebug("Clearing flexi light state because it was disabled for room '{room}'.", Name);
                    ClearAutoTurnOff();
                    await RemoveIgnorePresence();
                    FlexiScenes.DeactivateScene();
                    InitiatedBy = InitiatedBy.NoOne;
                    await UpdateStateInHomeAssistant();
                }
                await _mqttEntityManager.SetStateAsync(switchName, state);
            });
    }

    private async Task RetrieveSateFromHomeAssistant()
    {
        IgnorePresenceUntil = !String.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.IgnorePresenceUntil) ? DateTime.Parse(EnabledSwitch.Attributes.IgnorePresenceUntil) : null;
        TurnOffAt = !String.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.TurnOffAt) ? DateTime.Parse(EnabledSwitch.Attributes.TurnOffAt) : null;
        InitiatedBy = !String.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.InitiatedBy) ? Enum.Parse<InitiatedBy>(EnabledSwitch.Attributes.InitiatedBy) : InitiatedBy.NoOne;

        if (!String.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.CurrentFlexiScene))
        {
            var flexiScene = FlexiScenes.GetByName(EnabledSwitch.Attributes.CurrentFlexiScene);
            if (flexiScene != null)
                FlexiScenes.SetCurrentScene(flexiScene);
        }

        if (!String.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.InitialFlexiScene))
        {
            var flexiScene = FlexiScenes.GetByName(EnabledSwitch.Attributes.InitialFlexiScene);
            if (flexiScene != null)
                FlexiScenes.SetCurrentScene(flexiScene);
        }

        _logger.LogDebug("Retrieved flexilight state from Home assistant for room '{room}'.", Name);

        await ScheduleTurnOffAt();
        await ScheduleClearIgnorePresence();
    }


    private async Task UpdateStateInHomeAssistant()
    {
        if (!IsRoomEnabled())
            return;

        var attributes = new EnabledSwitchAttributes
        {
            IgnorePresenceUntil = IgnorePresenceUntil?.ToString("O"),
            TurnOffAt = TurnOffAt?.ToString("O"),
            InitiatedBy = InitiatedBy.ToString(),
            CurrentFlexiScene = FlexiScenes.Current?.Name,
            InitialFlexiScene = FlexiScenes.Initial?.Name,
            LastUpdated = DateTime.Now.ToString("O"),
            Icon = "mdi:palette"
        };
        await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
        _logger.LogTrace("Updated flexilight state for room '{room}' in Home assistant to {attr}", Name, attributes);
    }


    private bool IsRoomEnabled()
    {
        return EnabledSwitch.IsOn();
    }

    private async Task<bool> ExecuteFlexiScene(FlexiScene flexiScene, InitiatedBy initiatedBy, Boolean autoTransition = false, Boolean overwriteInitialScene = true)
    {
        _logger.LogInformation("{Room}: Will execute flexi scene {flexiScene}. Triggered by {InitiatedBy}.", Name, flexiScene.Name, initiatedBy);
        FlexiScenes.SetCurrentScene(flexiScene, overwriteInitialScene);
        InitiatedBy = initiatedBy;

        return await ExecuteActions(flexiScene.Actions, autoTransition);
    }
    private async Task ExecuteOffActions()
    {
        _logger.LogDebug("{Room}: Executed off actions.", Name);

        FlexiScenes.DeactivateScene();
        InitiatedBy = InitiatedBy.NoOne;

        if (IgnorePresenceAfterOffDuration != TimeSpan.Zero)
            IgnorePresenceUntil = DateTime.Now.Add(IgnorePresenceAfterOffDuration);

        await ScheduleClearIgnorePresence();

        await ExecuteActions(OffActions);
        ClearAutoTurnOff();
        await UpdateStateInHomeAssistant();
    }

    private async Task ScheduleClearIgnorePresence()
    {
        if (IgnorePresenceUntil == null)
            return;

        var remainingTime = IgnorePresenceUntil.Value - DateTime.Now;

        if (remainingTime > TimeSpan.Zero)
        {
            ClearIgnorePresenceSchedule?.Dispose();
            ClearIgnorePresenceSchedule = _scheduler.ScheduleAsync(remainingTime, async (_, _) => await RemoveIgnorePresence());

            _logger.LogDebug("{Room}: Presence will be ignored until {IgnorePresenceUntil}.", Name, IgnorePresenceUntil?.ToString("T"));
        }
        else
        {
            _logger.LogDebug("{Room}: Ignore presence should already have been cleared at {IgnorePresenceUntil} will clear it now.", Name, IgnorePresenceUntil?.ToString("T"));
            await RemoveIgnorePresence();
        }
    }



    private async Task ExecuteDoubleClickActions()
    {
        InitiatedBy = InitiatedBy.Switch;
        await ExecuteActions(DoubleClickActions);
    }
    private async Task ExecuteTripleClickActions()
    {
        InitiatedBy = InitiatedBy.Switch;
        await ExecuteActions(TripleClickActions);
    }

    private async Task ExecuteLongClickActions()
    {
        InitiatedBy = InitiatedBy.Switch;
        await ExecuteActions(LongClickActions);
    }

    private async Task ExecuteUberLongClickActions()
    {
        InitiatedBy = InitiatedBy.Switch;
        await ExecuteActions(UberLongClickActions);
    }

    private async Task SetTurnOffAt(FlexiScene flexiScene)
    {
        var timeSpan = InitiatedBy switch
        {
            InitiatedBy.Motion => flexiScene.TurnOffAfterIfTriggeredByMotionSensor,
            InitiatedBy.Switch => flexiScene.TurnOffAfterIfTriggeredBySwitch,
            _ => throw new ArgumentOutOfRangeException()
        };

        TurnOffAt = DateTime.Now.Add(timeSpan);
        await ScheduleTurnOffAt();
    }

    private async Task ScheduleTurnOffAt()
    {
        if (TurnOffAt == null)
            return;

        var remainingTime = TurnOffAt.Value - DateTime.Now;

        if (remainingTime > TimeSpan.Zero)
        {
            TurnOffSchedule?.Dispose();
            TurnOffSchedule = _scheduler.ScheduleAsync(remainingTime, async (_, _) => await ExecuteOffActions());

            _logger.LogTrace("{Room}: Off actions will be executed at {TurnOffAt} (Set by {InitiatedBy}).", Name, TurnOffAt?.ToString("T"), InitiatedBy);
        }
        else
        {
            _logger.LogDebug("{Room}: Off actions should have been executed at {TurnOffAt} will execute them now (Set by {InitiatedBy}).", Name, TurnOffAt?.ToString("T"), InitiatedBy);
            await ExecuteOffActions();
        }
    }

    private void ClearAutoTurnOff()
    {
        _logger.LogTrace("{Room}: Off actions will no longer be executed. Probably because the OFF actions were just executed or a motion sensor is active.", Name);
        TurnOffAt = null;
        TurnOffSchedule?.Dispose();
    }

    private async Task<Boolean> ExecuteActions(IReadOnlyCollection<Action> actions, Boolean autoTransition = false)
    {
        var offActionsExecuted = false;
        foreach (var action in actions)
        {
            if (action is ExecuteOffActionsAction)
            {
                await ExecuteOffActions();
                offActionsExecuted = true;
            }
            else
                await action.Execute(autoTransition);
        }

        return offActionsExecuted;
    }
    private async Task OffSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        await ExecuteOffActions();
    }

    private async Task MotionSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        //If motion / presence is detected and there is an active scene do nothing but cancel auto turn off
        if (FlexiScenes.Current != null)
        {
            ClearAutoTurnOff();
            return;
        }

        await ExecuteFlexiSceneOnMotion();
    }

    private async Task Switch_Clicked(object? sender, SwitchEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        _logger.LogDebug("{Room}: Click triggered for switch {EntityId}", Name, e.Sensor.EntityId);
        if (FlexiScenes.Current == null)
        {
            if (FlexiSceneThatShouldActivate != null)
            {
                var offActionsExecuted = await ExecuteFlexiScene(FlexiSceneThatShouldActivate, InitiatedBy.Switch);

                if (offActionsExecuted)
                    return;

                await SetTurnOffAt(FlexiSceneThatShouldActivate);
                await UpdateStateInHomeAssistant();
            }
            else
            {
                _logger.LogWarning("{Room}: Click was detected but found no flexi scene that could be executed.", Name);
            }

            return;
        }

        if (InitiatedBy == InitiatedBy.Motion && InitialClickAfterMotionBehaviour == InitialClickAfterMotionBehaviour.ChangeOffDurationOnly)
        {
            InitiatedBy = InitiatedBy.Switch;
            await SetTurnOffAt(FlexiScenes.Current);
        }
        else
        {
            var nextFlexiScene = FlexiScenes.Next;
            var offActionsExecuted = await ExecuteFlexiScene(nextFlexiScene, InitiatedBy.Switch, false, false);

            if (offActionsExecuted)
                return;

            await SetTurnOffAt(nextFlexiScene);
        }

        await UpdateStateInHomeAssistant();
    }

    private async Task Switch_DoubleClicked(object? sender, SwitchEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        _logger.LogDebug("{Room}: Double click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!DoubleClickActions.Any())
            return;

        await ExecuteDoubleClickActions();
    }

    private async Task Switch_TripleClicked(object? sender, SwitchEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        _logger.LogDebug("{Room}: Triple click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!TripleClickActions.Any())
            return;

        await ExecuteTripleClickActions();
    }
    private async Task Switch_LongClicked(object? sender, SwitchEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        _logger.LogDebug("{Room}: Long click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!LongClickActions.Any())
            return;

        await ExecuteLongClickActions();
    }
    private async Task Switch_UberLongClicked(object? sender, SwitchEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        _logger.LogDebug("{Room}: Uber long click triggered for switch {EntityId}", Name, e.Sensor.EntityId);
        if (!LongClickActions.Any() && !UberLongClickActions.Any())
            return;

        if (UberLongClickActions.Any())
        {
            await ExecuteUberLongClickActions();
            return;
        }

        //Fall back to long click if there are no uber long click actions
        if (LongClickActions.Any())
        {
            await ExecuteLongClickActions();
        }
    }
    private async Task ExecuteFlexiSceneOnMotion()
    {
        if (IlluminanceThreshold != null && IlluminanceSensors.All(x => x.State > IlluminanceThreshold))
        {
            _logger.LogTrace(
                "{Room}: Motion sensor saw something moving but did not turn on lights because all illuminance sensors [{IlluminanceSensorValues}] are above threshold of {IlluminanceThreshold}", Name, String.Join(", ", IlluminanceSensors.Select(x => $"{x.EntityId} - {x.State} lux")), IlluminanceThreshold);
            return;
        }

        if (IgnorePresenceUntil != null && IgnorePresenceUntil > DateTime.Now)
        {
            _logger.LogDebug("{Room}: Motion sensor saw something moving but did not turn on lights because presence is ignored until {IgnorePresenceUntil}", Name, IgnorePresenceUntil?.ToString("T"));
            return;
        }

        if (FlexiSceneThatShouldActivate != null)
        {
            await ExecuteFlexiScene(FlexiSceneThatShouldActivate, InitiatedBy.Motion);
        }
        else
        {
            _logger.LogWarning("{Room}: Motion was detected but found no flexi scenes that could be executed.", Name);
        }

        await UpdateStateInHomeAssistant();
    }

    private async Task MotionSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        var allMotionSensorsOff = MotionSensors.All(x => x.IsOff());

        if (allMotionSensorsOff && FlexiScenes.Current != null)
        {
            await SetTurnOffAt(FlexiScenes.Current);
            await UpdateStateInHomeAssistant();
        }
    }

    private async Task Sensor_WentAboveThreshold(object? sender, NumericSensorEventArgs e)
    {
        if (!IsRoomEnabled())
            return;

        if (AutoSwitchOffAboveIlluminance && IlluminanceSensors.All(x => x.State > IlluminanceThreshold) && FlexiScenes.Current != null)
        {
            _logger.LogDebug("{Room}: Executed off actions. Because a motion sensor exceeded the illuminance threshold ({e.New.State} lux)", Name);
            await ExecuteOffActions();
        }
    }

    private async Task ExecuteFlexiSceneOnAutoTransition()
    {
        if (!IsRoomEnabled())
            return;

        if (!AutoTransition)
            return;

        if (FlexiScenes.Current == null && !FullyAutomated)
            return;

        //current flexi scene still valid
        if (FlexiScenes.Current != null && FlexiScenes.Current.CanActivate(FlexiSceneSensors))
        {
            _logger.LogInformation("{Room}: Operating mode of a scene changed but current scene is still valid. Will not auto transition", Name);
            return;
        }

        if (FlexiSceneThatShouldActivate != null)
        {
            _logger.LogInformation("{Room}: Auto transition was triggered.", Name);
            await ExecuteFlexiScene(FlexiSceneThatShouldActivate, FullyAutomated ? InitiatedBy.FullyAutomated : InitiatedBy, true);
        }
        else
        {
            switch (AutoTransitionTurnOffIfNoValidSceneFound)
            {
                case true when FlexiScenes.Current != null:
                    await ExecuteOffActions();
                    break;
                case true when FlexiScenes.Current == null:
                    break;
                default:
                    _logger.LogInformation("{Room}: Auto transition was triggered but no flexi scene was found to transition to.", Name);
                    break;
            }
        }

        await UpdateStateInHomeAssistant();
    }

    public void Guard()
    {
        _scheduler.RunEvery(TimeSpan.FromSeconds(1), _scheduler.Now, async () =>
        {
            //Can only happen executes on startup, otherwise we receive a motion detected event
            await CheckForMotion();
        });
    }


    private async Task RemoveIgnorePresence()
    {
        IgnorePresenceUntil = null;
        await UpdateStateInHomeAssistant();
    }

    private async Task CheckForMotion()
    {
        if (!IsRoomEnabled())
            return;

        if (MotionSensors.All(x => x.IsOff()))
            return;

        if (FlexiScenes.Current != null)
            return;

        if (IgnorePresenceUntil != null)
            return;

        await ExecuteFlexiSceneOnMotion();
    }
}