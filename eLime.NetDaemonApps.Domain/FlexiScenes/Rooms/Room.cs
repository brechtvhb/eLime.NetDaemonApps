using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Conditions;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Actions;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

public class Room : IAsyncDisposable
{
    public string Name { get; }
    private bool Enabled { get; set; }
    private DateTimeOffset? LastChangedAt { get; set; }

    public bool AutoTransition { get; }
    public bool AutoTransitionTurnOffIfNoValidSceneFound { get; }
    private bool FullyAutomated { get; }
    private readonly DebounceDispatcher? AutoTransitionDebounceDispatcher;

    public InitialClickAfterMotionBehaviour InitialClickAfterMotionBehaviour { get; }
    public Int32? IlluminanceThreshold { get; }
    public Int32? IlluminanceLowerThreshold { get; }
    public bool AutoSwitchOffAboveIlluminance { get; }
    public TimeSpan IgnorePresenceAfterOffDuration { get; }
    public DateTimeOffset? TurnOffAt { get; private set; }
    private IDisposable? TurnOffSchedule { get; set; }
    private DateTimeOffset? IgnorePresenceUntil { get; set; }
    private IDisposable? ClearIgnorePresenceSchedule { get; set; }
    private IDisposable? GuardTask { get; set; }
    private IDisposable? SimulateTask { get; set; }
    private IDisposable SwitchDisposable { get; set; }
    private IDisposable DropdownChangedCommandHandler { get; set; }

    public InitiatedBy InitiatedBy { get; private set; } = InitiatedBy.NoOne;

    private readonly List<BinarySensor> _offSensors = new();
    public IReadOnlyCollection<BinarySensor> OffSensors => _offSensors.AsReadOnly();

    public void AddOffSensor(BinarySensor sensor)
    {
        sensor.TurnedOn += OffSensor_TurnedOn;
        _offSensors.Add(sensor);
    }

    private readonly List<IlluminanceSensor> _illuminanceSensors = new();
    public IReadOnlyCollection<IlluminanceSensor> IlluminanceSensors => _illuminanceSensors.AsReadOnly();

    public void AddIlluminanceSensor(IlluminanceSensor sensor)
    {
        sensor.WentAboveThreshold += Sensor_WentAboveThreshold;
        _illuminanceSensors.Add(sensor);
    }

    private readonly List<MotionSensor> _motionSensors = new();
    public IReadOnlyCollection<MotionSensor> MotionSensors => _motionSensors.AsReadOnly();

    public void AddMotionSensor(MotionSensor sensor)
    {
        sensor.TurnedOn += MotionSensor_TurnedOn;
        sensor.TurnedOff += MotionSensor_TurnedOff;
        _motionSensors.Add(sensor);
    }

    private readonly List<ISwitch> _switches = new();
    public IReadOnlyCollection<ISwitch> Switches => _switches.AsReadOnly();

    public void AddSwitches(List<ISwitch> switches)
    {
        foreach (var sensor in switches)
        {
            sensor.Clicked += Switch_Clicked;
            sensor.DoubleClicked += Switch_DoubleClicked;
            sensor.TripleClicked += Switch_TripleClicked;
            sensor.LongClicked += Switch_LongClicked;
            sensor.UberLongClicked += Switch_UberLongClicked;
            _switches.Add(sensor);
        }
    }

    public BinarySensor? SimulatePresenceSensor;
    public TimeSpan SimulatePresenceIgnoreDuration;

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
            binarySensor.TurnedOn += DebounceAutoTransitionAsync;
            binarySensor.TurnedOff += DebounceAutoTransitionAsync;
            _flexiSceneSensors.Add(binarySensor);
        }
    }

    private async void DebounceAutoTransitionAsync(object? sender, BinarySensorEventArgs e)
    {
        if (AutoTransitionDebounceDispatcher == null)
        {
            await ExecuteFlexiSceneOnAutoTransition();
            return;
        }
        await AutoTransitionDebounceDispatcher.DebounceAsync(ExecuteFlexiSceneOnAutoTransition);
    }

    public FlexiScenes FlexiScenes { get; }
    public FlexiScene? FlexiSceneThatShouldActivate => FlexiScenes.GetSceneThatShouldActivate(FlexiSceneSensors);


    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;


    public Room(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, RoomConfig config, TimeSpan autoTransitionDebounce)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        Name = config.Name;

        if (autoTransitionDebounce != TimeSpan.Zero)
            AutoTransitionDebounceDispatcher = new(autoTransitionDebounce);

        if (!(config.Enabled ?? true))
        {
            FlexiScenes = new FlexiScenes(new List<FlexiScene>());
            return;
        }

        AutoTransition = config.AutoTransition;
        AutoTransitionTurnOffIfNoValidSceneFound = config.AutoTransitionTurnOffIfNoValidSceneFound;
        SimulatePresenceSensor = !String.IsNullOrWhiteSpace(config.SimulatePresenceSensor) ? new BinarySensor(_haContext, config.SimulatePresenceSensor) : null;
        SimulatePresenceIgnoreDuration = config.SimulatePresenceIgnoreDuration ?? TimeSpan.FromMinutes(3);
        InitialClickAfterMotionBehaviour = config.InitialClickAfterMotionBehaviour == Config.FlexiLights.InitialClickAfterMotionBehaviour.ChangeOffDurationOnly ? InitialClickAfterMotionBehaviour.ChangeOffDurationOnly : InitialClickAfterMotionBehaviour.ChangeOFfDurationAndGoToNextFlexiScene;
        IlluminanceThreshold = config.IlluminanceThreshold;
        IlluminanceLowerThreshold = config.IlluminanceLowerThreshold ?? IlluminanceThreshold;
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

                var sensor = IlluminanceSensor.Create(_haContext, sensorId, config.IlluminanceThreshold, config.IlluminanceLowerThreshold);
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

        RetrieveSate().RunSync();
        EnsureSensorsExist().RunSync();

        _logger.LogInformation("{Room}: Initialized with scenes: {Scenes}.", Name, String.Join(", ", FlexiScenes.All.Select(x => x.Name)));

        if (FullyAutomated)
            ExecuteFlexiSceneOnAutoTransition().RunSync();

        //Should this still be a periodic task? Can only happen on startup, otherwise we receive a motion detected event.
        GuardTask = _scheduler.RunEvery(TimeSpan.FromSeconds(5), _scheduler.Now, CheckForMotion);
        SimulateTask = _scheduler.RunEvery(TimeSpan.FromSeconds(15), _scheduler.Now, SimulatePresenceIfNeeded);
    }

    private async Task EnsureSensorsExist()
    {
        var baseName = $"sensor.flexilights_{Name.MakeHaFriendly()}";
        var switchName = $"switch.flexilights_{Name.MakeHaFriendly()}";
        var selectName = $"select.flexilights_{Name.MakeHaFriendly()}_scene";

        if (_haContext.Entity(switchName).State == null)
        {
            _logger.LogDebug("Creating Entities for room '{room}' in home assistant.", Name);
            var enabledSwitchOptions = new EnabledSwitchAttributes { Icon = "fapro:palette", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Flexi lights - {Name}", DeviceClass: "switch", Persist: true), enabledSwitchOptions);
            Enabled = true;
            await _mqttEntityManager.SetStateAsync(switchName, "ON");

            var initiatedByOptions = new EnumSensorOptions { Icon = "hue:motion-sensor-movement", Device = GetDevice(), Options = Enum<InitiatedBy>.AllValuesAsStringList() };
            await _mqttEntityManager.CreateAsync($"{baseName}_initiated_by", new EntityCreationOptions(UniqueId: $"{baseName}_initiated_by", Name: $"Initiated by", DeviceClass: "enum", Persist: true), initiatedByOptions);
        }

        var lastChangeOptions = new EntityOptions { Icon = "mdi:calendar-end", Device = GetDevice() };
        await _mqttEntityManager.CreateAsync($"{baseName}_last_changed_at", new EntityCreationOptions(UniqueId: $"{baseName}_last_changed_at", Name: $"Last changed at", Persist: true), lastChangeOptions);

        var scenes = new List<String> { "Off" };
        scenes.AddRange(FlexiScenes.All.Select(x => x.Name));

        var selectOptions = new SelectOptions { Icon = "fapro:palette", Options = scenes, Device = GetDevice() };
        await _mqttEntityManager.CreateAsync(selectName, new EntityCreationOptions(UniqueId: selectName, Name: "Scene", DeviceClass: "select", Persist: true), selectOptions);
        await _mqttEntityManager.SetStateAsync($"select.flexilights_{Name.MakeHaFriendly()}_scene", FlexiScenes.Current?.Name ?? "Off");

        var observer = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
        SwitchDisposable = observer.SubscribeAsync(EnabledSwitchHandler());

        var selectObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(selectName);
        DropdownChangedCommandHandler = selectObserver.SubscribeAsync(DropDownChanged());
    }

    private Func<string, Task> EnabledSwitchHandler()
    {
        return async state =>
        {
            _logger.LogDebug("Setting flexi lights state for room '{room}' to {state}.", Name, state);

            if (state == "OFF")
            {
                _logger.LogDebug("Clearing flexi light state because it was disabled for room '{room}'.", Name);
                ClearAutoTurnOff();
                await RemoveIgnorePresence();
                FlexiScenes.DeactivateScene(_scheduler.Now);
                InitiatedBy = InitiatedBy.NoOne;
            }
            Enabled = state == "ON";
            await UpdateStateInHomeAssistant();
        };
    }

    private Func<string, Task> DropDownChanged()
    {
        return async state =>
        {
            if (state == "Off")
            {
                await ExecuteOffActions();
                return;
            }

            var flexiScene = FlexiScenes.GetByName(state);

            if (flexiScene == null)
                return;

            var offActionsExecuted = await ExecuteFlexiScene(flexiScene, InitiatedBy.Switch);

            if (offActionsExecuted)
                return;

            await SetTurnOffAt(flexiScene);
            await UpdateStateInHomeAssistant();
        };
    }


    public Device GetDevice()
    {
        return new Device { Identifiers = new List<string> { $"flexiscene.{Name.MakeHaFriendly()}" }, Name = "Flexi scene: " + Name, Manufacturer = "Me" };
    }


    private async Task RetrieveSate()
    {
        var flexiSceneFileStorage = _fileStorage.Get<FlexiSceneFileStorage>("FlexiScenes", Name.MakeHaFriendly());

        Enabled = flexiSceneFileStorage?.Enabled ?? false;

        IgnorePresenceUntil = flexiSceneFileStorage?.IgnorePresenceUntil;
        TurnOffAt = flexiSceneFileStorage?.TurnOffAt;
        InitiatedBy = flexiSceneFileStorage?.InitiatedBy ?? InitiatedBy.NoOne;
        FlexiScenes.Changes = flexiSceneFileStorage?.Changes.ToList() ?? new List<FlexiSceneChange>();

        if (!String.IsNullOrWhiteSpace(flexiSceneFileStorage?.ActiveFlexiScene))
        {
            var activeScene = FlexiScenes.GetByName(flexiSceneFileStorage.ActiveFlexiScene);
            if (activeScene != null)
            {
                var initialScene = flexiSceneFileStorage.InitialFlexiScene != null ? FlexiScenes.GetByName(flexiSceneFileStorage.InitialFlexiScene) : null;
                FlexiScenes.Initialize(activeScene, initialScene);
            }
        }

        _logger.LogDebug("Retrieved flexilight state from file storage for room '{room}'.", Name);

        await ScheduleTurnOffAt();
        await ScheduleClearIgnorePresence();
    }


    private async Task UpdateStateInHomeAssistant()
    {
        var baseName = $"sensor.flexilights_{Name.MakeHaFriendly()}";

        LastChangedAt = _scheduler.Now;

        var attributes = new EnabledSwitchAttributes
        {
            LastUpdated = LastChangedAt?.ToString("O"),
            Device = GetDevice()
        };

        var task1 = _mqttEntityManager.SetAttributesAsync($"switch.flexilights_{Name.MakeHaFriendly()}", attributes);
        var task2 = _mqttEntityManager.SetStateAsync($"switch.flexilights_{Name.MakeHaFriendly()}", Enabled ? "ON" : "OFF");
        var task3 = _mqttEntityManager.SetStateAsync($"{baseName}_initiated_by", InitiatedBy.ToString());
        var task4 = _mqttEntityManager.SetStateAsync($"{baseName}_last_changed_at", FlexiScenes.Changes.LastOrDefault()?.ChangedAt.ToString("O"));
        var task5 = _mqttEntityManager.SetStateAsync($"select.flexilights_{Name.MakeHaFriendly()}_scene", FlexiScenes.Current?.Name ?? "Off");

        await Task.WhenAll(task1, task2, task3, task4, task5);

        _fileStorage.Save("FlexiScenes", Name.MakeHaFriendly(), ToFileStorage());
        _logger.LogTrace("Updated flexilight state for room '{room}' in Home assistant.", Name);
    }

    internal FlexiSceneFileStorage ToFileStorage()
    {
        return new()
        {
            Enabled = Enabled,
            IgnorePresenceUntil = IgnorePresenceUntil,
            TurnOffAt = TurnOffAt,
            InitiatedBy = InitiatedBy,
            ActiveFlexiScene = FlexiScenes.Current?.Name,
            InitialFlexiScene = FlexiScenes.Initial?.Name,
            Changes = FlexiScenes.Changes
        };
    }

    private async Task<bool> ExecuteFlexiScene(FlexiScene flexiScene, InitiatedBy initiatedBy, Boolean autoTransition = false, Boolean overwriteInitialScene = true)
    {
        _logger.LogInformation("{Room}: Will execute flexi scene {flexiScene}. Triggered by {InitiatedBy}.", Name, flexiScene.Name, initiatedBy);
        FlexiScenes.SetCurrentScene(_scheduler.Now, flexiScene, overwriteInitialScene);
        InitiatedBy = initiatedBy;

        return await ExecuteActions(flexiScene.Actions, autoTransition);
    }

    private async Task ExecuteOffActions(bool triggeredByManualAction = true)
    {
        _logger.LogDebug("{Room}: Executed off actions.", Name);

        FlexiScenes.DeactivateScene(_scheduler.Now);
        InitiatedBy = InitiatedBy.NoOne;

        //Only ignore presence if triggered by manual action
        if (triggeredByManualAction && IgnorePresenceAfterOffDuration != TimeSpan.Zero)
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
            InitiatedBy.FullyAutomated => flexiScene.TurnOffAfterIfTriggeredBySwitch,
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
            TurnOffSchedule = _scheduler.ScheduleAsync(remainingTime, async (_, _) => await ExecuteOffActions(triggeredByManualAction: false));

            _logger.LogTrace("{Room}: Off actions will be executed at {TurnOffAt} (Set by {InitiatedBy}).", Name, TurnOffAt?.ToString("T"), InitiatedBy);
        }
        else
        {
            _logger.LogDebug("{Room}: Off actions should have been executed at {TurnOffAt} will execute them now (Set by {InitiatedBy}).", Name, TurnOffAt?.ToString("T"), InitiatedBy);
            await ExecuteOffActions(triggeredByManualAction: false);
        }
    }

    private void ClearAutoTurnOff()
    {
        _logger.LogTrace("{Room}: Off actions will no longer be executed. Probably because the OFF actions were just executed or a motion sensor is active.", Name);
        TurnOffAt = null;
        TurnOffSchedule?.Dispose();
        TurnOffSchedule = null;
    }

    private async Task<Boolean> ExecuteActions(IReadOnlyCollection<Action> actions, Boolean autoTransition = false)
    {
        var offActionsExecuted = false;
        foreach (var action in actions)
        {
            if (action is ExecuteOffActionsAction)
            {
                await ExecuteOffActions(triggeredByManualAction: true);
                offActionsExecuted = true;
            }
            else
                await action.Execute(autoTransition);
        }

        return offActionsExecuted;
    }
    private async void OffSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (!Enabled)
            return;

        await ExecuteOffActions(triggeredByManualAction: true);
    }

    private async void MotionSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (!Enabled)
            return;

        //If motion / presence is detected and there is an active scene do nothing but cancel auto turn off
        if (FlexiScenes.Current != null)
        {
            ClearAutoTurnOff();
            await UpdateStateInHomeAssistant();
            return;
        }

        await ExecuteFlexiSceneOnMotion();
    }

    private async void Switch_Clicked(object? sender, SwitchEventArgs e)
    {
        if (!Enabled)
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

    private async void Switch_DoubleClicked(object? sender, SwitchEventArgs e)
    {
        if (!Enabled)
            return;

        _logger.LogDebug("{Room}: Double click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!DoubleClickActions.Any())
            return;

        await ExecuteDoubleClickActions();
    }

    private async void Switch_TripleClicked(object? sender, SwitchEventArgs e)
    {
        if (!Enabled)
            return;

        _logger.LogDebug("{Room}: Triple click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!TripleClickActions.Any())
            return;

        await ExecuteTripleClickActions();
    }
    private async void Switch_LongClicked(object? sender, SwitchEventArgs e)
    {
        if (!Enabled)
            return;

        _logger.LogDebug("{Room}: Long click triggered for switch {EntityId}", Name, e.Sensor.EntityId);

        if (!LongClickActions.Any())
            return;

        await ExecuteLongClickActions();
    }
    private async void Switch_UberLongClicked(object? sender, SwitchEventArgs e)
    {
        if (!Enabled)
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
        if (IlluminanceLowerThreshold != null && IlluminanceSensors.All(x => x.State > IlluminanceLowerThreshold))
        {
            _logger.LogTrace(
                "{Room}: Motion sensor saw something moving but did not turn on lights because all illuminance sensors [{IlluminanceSensorValues}] are above threshold of {IlluminanceThreshold} lux.", Name, String.Join(", ", IlluminanceSensors.Select(x => $"{x.EntityId} - {x.State} lux")), IlluminanceLowerThreshold);
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
            await UpdateStateInHomeAssistant();
        }
        else
        {
            _logger.LogWarning("{Room}: Motion was detected but found no flexi scenes that could be executed.", Name);
        }
    }

    private async void MotionSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        if (!Enabled)
            return;

        var allMotionSensorsOff = MotionSensors.All(x => x.IsOff());

        if (allMotionSensorsOff && FlexiScenes.Current != null)
        {
            await SetTurnOffAt(FlexiScenes.Current);
            await UpdateStateInHomeAssistant();
        }
    }

    private async void Sensor_WentAboveThreshold(object? sender, NumericSensorEventArgs e)
    {
        if (!Enabled)
            return;

        if (AutoSwitchOffAboveIlluminance && IlluminanceSensors.All(x => x.State > IlluminanceThreshold) && FlexiScenes.Current != null)
        {
            _logger.LogDebug("{Room}: Executed off actions. Because a illuminance sensor exceeded the illuminance threshold ({e.New.State} > {IlluminanceThreshold} lux) ", Name);
            await ExecuteOffActions(triggeredByManualAction: false);
        }
    }

    private async Task ExecuteFlexiSceneOnAutoTransition()
    {
        if (!Enabled)
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
                    await ExecuteOffActions(triggeredByManualAction: false);
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

    private async Task RemoveIgnorePresence()
    {
        IgnorePresenceUntil = null;
        await UpdateStateInHomeAssistant();
    }

    private async void CheckForMotion()
    {
        if (!Enabled)
            return;

        if (MotionSensors.All(x => x.IsOff()))
            return;

        if (MotionSensors.Any(x => x.State == "unavailable"))
            return;

        if (FlexiScenes.Current != null)
            return;

        if (IgnorePresenceUntil != null)
            return;

        await ExecuteFlexiSceneOnMotion();
    }

    private async void SimulatePresenceIfNeeded()
    {
        if (!Enabled)
            return;

        if (SimulatePresenceSensor == null || SimulatePresenceSensor.State == "unavailable" || SimulatePresenceSensor.IsOff())
            return;

        var flexiSceneToSimulate = FlexiScenes.GetFlexiSceneToSimulate(_scheduler.Now);

        if (LastChangedAt?.Add(SimulatePresenceIgnoreDuration) > _scheduler.Now)
            return;

        if (FlexiScenes.Current == flexiSceneToSimulate)
            return;

        if (flexiSceneToSimulate == null)
        {
            _logger.LogDebug("{Room}: Simulating off actions", Name);
            await ExecuteOffActions();
            return;
        }

        _logger.LogDebug("{Room}: Simulating presence.", Name);
        var offActionsExecuted = await ExecuteFlexiScene(flexiSceneToSimulate, InitiatedBy.FullyAutomated);

        if (offActionsExecuted)
            return;

        await SetTurnOffAt(flexiSceneToSimulate);
        await UpdateStateInHomeAssistant();
    }


    public ValueTask DisposeAsync()
    {
        _logger.LogDebug("{Room}: Disposing.", Name);

        foreach (var sensor in _offSensors)
        {
            sensor.TurnedOn -= OffSensor_TurnedOn;
            sensor.Dispose();
        }

        foreach (var sensor in _illuminanceSensors)
        {
            sensor.WentAboveThreshold -= Sensor_WentAboveThreshold;
            sensor.Dispose();
        }

        foreach (var sensor in _motionSensors)
        {
            sensor.TurnedOn -= MotionSensor_TurnedOn;
            sensor.TurnedOff -= MotionSensor_TurnedOff;
            sensor.Dispose();
        }

        foreach (var sensor in _switches)
        {
            sensor.Clicked -= Switch_Clicked;
            sensor.DoubleClicked -= Switch_DoubleClicked;
            sensor.TripleClicked -= Switch_TripleClicked;
            sensor.LongClicked -= Switch_LongClicked;
            sensor.UberLongClicked -= Switch_UberLongClicked;
            sensor.Dispose();
        }

        foreach (var sensor in _flexiSceneSensors)
        {
            if (sensor is BinarySensor binarySensor)
            {
                binarySensor.TurnedOn -= DebounceAutoTransitionAsync;
                binarySensor.TurnedOff -= DebounceAutoTransitionAsync;
                binarySensor.Dispose();
            }
        }

        GuardTask?.Dispose();

        SimulatePresenceSensor?.Dispose();
        SimulateTask?.Dispose();

        ClearIgnorePresenceSchedule?.Dispose();
        TurnOffSchedule?.Dispose();
        DropdownChangedCommandHandler?.Dispose();
        SwitchDisposable?.Dispose();

        _logger.LogDebug("{Room}: Disposed.", Name);

        return ValueTask.CompletedTask;
    }
}

internal class FlexiSceneChange
{
    public DateTimeOffset ChangedAt { get; set; }
    public String? Scene { get; set; }
}