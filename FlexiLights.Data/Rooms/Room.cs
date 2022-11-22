using FlexiLights.Config;
using FlexiLights.Data.BinarySensors;
using FlexiLights.Data.Helper;
using FlexiLights.Data.Numeric;
using FlexiLights.Data.Rooms.Actions;
using FlexiLights.Data.Rooms.Evaluations;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = FlexiLights.Data.Rooms.Actions.Action;
using Switch = FlexiLights.Data.BinarySensors.Switch;

namespace FlexiLights.Data.Rooms;

public class Room
{
    private const string NONE = "none";
    private const string OFF = "off";
    private const string ON = "on";

    public string? Name { get; private init; }
    public bool AutoTransition { get; private init; }
    private readonly DebounceDispatcher AutoTransitionDebounceDispatcher = new(TimeSpan.FromSeconds(1));

    public InitialClickAfterMotionBehaviour InitialClickAfterMotionBehaviour { get; private init; }
    public Int32? IlluminanceThreshold { get; private init; }
    public bool AutoSwitchOffAboveIlluminance { get; private init; }
    public TimeSpan IgnorePresenceAfterOffDuration { get; private init; }
    private DateTime? TurnOffAt { get; set; }
    private DateTime? IgnorePresenceUntil { get; set; }
    private String CurrentFlexiScene { get; set; } = NONE;
    private InitiatedBy InitiatedBy { get; set; } = InitiatedBy.NoOne;

    private readonly List<BinarySensor> _offSensors = new();
    public IReadOnlyCollection<BinarySensor> OffSensors => _offSensors.AsReadOnly();

    public void AddOffSensor(BinarySensor sensor)
    {
        sensor.TurnedOn += async (s, e) => await OffSensor_TurnedOn(s, e);
        _offSensors.Add(sensor);
    }

    private readonly List<Lights.Light> _lights = new();
    public IReadOnlyCollection<Lights.Light> Lights => _lights.AsReadOnly();
    public void AddLight(Lights.Light light) => _lights.Add(light);


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
        sensor.TurnedOff += MotionSensor_TurnedOff;
        _motionSensors.Add(sensor);
    }

    private readonly List<Switch> _switches = new();
    public IReadOnlyCollection<Switch> Switches => _switches.AsReadOnly();

    public void AddSwitch(Switch sensor)
    {
        sensor.Clicked += async (s, e) => await Switch_Clicked(s, e);
        sensor.DoubleClicked += async (s, e) => await Switch_DoubleClicked(s, e);
        sensor.TripleClicked += async (s, e) => await Switch_TripleClicked(s, e);
        sensor.LongClicked += async (s, e) => await Switch_LongClicked(s, e);
        sensor.UberLongClicked += async (s, e) => await Switch_UberLongClicked(s, e);
        _switches.Add(sensor);
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


    private readonly CircularReadOnlyList<FlexiScene> _flexiScenes = new();
    public IReadOnlyList<FlexiScene> FlexiScenes => _flexiScenes.AsReadOnly();
    public void AddFlexiScenes(FlexiScene flexiScene) => _flexiScenes.Add(flexiScene);


    private readonly IHaContext _haContext;
    private readonly ILogger _logger;

    public Room(IHaContext haContext, ILogger logger)
    {
        _haContext = haContext;
        _logger = logger;
    }

    public static Room Create(IHaContext context, ILogger logger, RoomConfig config)
    {
        var room = new Room(context, logger)
        {
            Name = config.Name,
            AutoTransition = config.AutoTransition,
            InitialClickAfterMotionBehaviour = config.InitialClickAfterMotionBehaviour == Config.InitialClickAfterMotionBehaviour.ChangeOffDurationOnly ? InitialClickAfterMotionBehaviour.ChangeOffDurationOnly : InitialClickAfterMotionBehaviour.ChangeOFfDurationAndGoToNextAutomation,
            IlluminanceThreshold = config.IlluminanceThreshold,
            AutoSwitchOffAboveIlluminance = config.AutoSwitchOffAboveIlluminance,
            IgnorePresenceAfterOffDuration = config.IgnorePresenceAfterOffDuration ?? TimeSpan.Zero
        };

        if (config.Lights == null || !config.Lights.Any())
            throw new Exception("Define at least one light");

        foreach (var lightId in config.Lights)
        {
            if (room.Lights.Any(x => x.EntityId == lightId))
                continue;

            var light = Data.Lights.Light.Create(context, lightId);
            room.AddLight(light);
        }


        if (config.OffSensors != null && config.OffSensors.Any())
        {
            foreach (var sensorId in config.OffSensors)
            {
                if (room.OffSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = BinarySensor.Create(context, sensorId);
                room.AddOffSensor(sensor);
            }
        }

        if (config.IlluminanceSensors != null && config.IlluminanceSensors.Any())
        {
            foreach (var sensorId in config.IlluminanceSensors)
            {
                if (room.IlluminanceSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = IlluminanceSensor.Create(context, sensorId, config.IlluminanceThreshold);
                room.AddIlluminanceSensor(sensor);
            }
        }

        if (config.MotionSensors != null && config.MotionSensors.Any())
        {
            foreach (var sensorId in config.MotionSensors)
            {
                if (room.MotionSensors.Any(x => x.EntityId == sensorId))
                    continue;

                var sensor = MotionSensor.Create(context, sensorId);
                room.AddMotionSensor(sensor);
            }
        }

        if (config.Switches != null && config.Switches.Any())
        {
            foreach (var switchId in config.Switches)
            {
                if (room.Switches.Any(x => x.EntityId == switchId))
                    continue;

                var sensor = Switch.Create(context, switchId, config.ClickInterval, config.LongClickDuration, config.UberLongClickDuration);
                room.AddSwitch(sensor);
            }
        }

        if (!room.Switches.Any() && !room.MotionSensors.Any())
            throw new Exception("Define at least one switch or motion sensor");

        if (config.OffActions == null || !config.OffActions.Any())
            throw new Exception("Define at least one off action");

        if (config.OffActions.Any(x => x.ExecuteOffActions))
            throw new ArgumentException("fuckturd");


        var offActions = config.OffActions.ConvertToDomainModel(context);
        room.AddOffActions(offActions);

        var doubleClickActions = config.DoubleClickActions.ConvertToDomainModel(context);
        room.AddDoubleClickActions(doubleClickActions);

        var tripleClickActions = config.TripleClickActions.ConvertToDomainModel(context);
        room.AddTripleClickActions(tripleClickActions);

        var longClickActions = config.LongClickActions.ConvertToDomainModel(context);
        room.AddLongClickActions(longClickActions);

        var uberLongClickActions = config.UberLongClickActions.ConvertToDomainModel(context);
        room.AddUberLongClickActions(uberLongClickActions);

        foreach (var flexiSceneConfig in config.FlexiScenes)
        {
            var flexiScene = FlexiScene.Create(context, flexiSceneConfig);

            if (!flexiScene.Evaluations.Any())
            {
                logger.LogDebug($"No evaluations were found on flexi scene '{flexiScene.Name}'. This will always resolve to true. Intended or configuration problem?");
            }
            room.AddFlexiScenes(flexiScene);

            foreach (var flexiSceneSensorId in flexiScene.GetSensorsIds())
            {
                if (room.FlexiSceneSensors.Any(x => x.EntityId == flexiSceneSensorId.Item1))
                    continue;

                if (flexiSceneSensorId.Item2 == EvaluationSensorType.Binary)
                {
                    var binarySensor = BinarySensor.Create(context, flexiSceneSensorId.Item1);
                    room.AddFlexiSceneSensor(binarySensor);
                }
            }

        }

        return room;
    }


    private async Task ExecuteFlexiScene(FlexiScene flexiScene, InitiatedBy initiatedBy, Boolean autoTransition = false)
    {
        _logger.LogInformation($"Will execute flexi scene {flexiScene.Name}. Triggered by {initiatedBy}.");
        CurrentFlexiScene = flexiScene.Name;
        InitiatedBy = initiatedBy;

        await ExecuteActions(flexiScene.Actions, autoTransition);
    }
    private async Task ExecuteOffActions()
    {
        CurrentFlexiScene = NONE;
        InitiatedBy = InitiatedBy.NoOne;
        IgnorePresenceUntil = DateTime.Now.Add(IgnorePresenceAfterOffDuration);
        await ExecuteActions(OffActions);
        RemoveTurnOffAt();
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

    private void SetTurnOffAt(FlexiScene gatedActions)
    {
        var timeSpan = InitiatedBy switch
        {
            InitiatedBy.Motion => gatedActions.TurnOffAfterIfTriggeredByMotionSensor,
            InitiatedBy.Switch => gatedActions.TurnOffAfterIfTriggeredBySwitch,
            _ => throw new ArgumentOutOfRangeException()
        };

        TurnOffAt = DateTime.Now.Add(timeSpan);
        _logger.LogDebug($"Off actions will be executed at {TurnOffAt:T}. Turn off at was set by {InitiatedBy}");
    }
    private void RemoveTurnOffAt()
    {
        _logger.LogDebug($"Off actions will no longer be executed. Probably because they were just executed or a motion sensor is active.");
        TurnOffAt = null;
    }

    private async Task ExecuteActions(IReadOnlyCollection<Action> actions, Boolean autoTransition = false)
    {
        foreach (var action in actions)
        {
            if (action is ExecuteOffActionsAction)
                await ExecuteOffActions();
            else
                await action.Execute(autoTransition);
        }
    }
    private async Task OffSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        await ExecuteOffActions();
    }

    private async Task MotionSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        await ExecuteFlexiSceneOnMotion(InitiatedBy.Motion);
    }

    private async Task Switch_Clicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Click triggered for switch {e.Sensor.EntityId}");
        if (CurrentFlexiScene == NONE)
        {
            var gatedActionsThatShouldActivate = FlexiScenes.FirstOrDefault(x => x.CanActivate(FlexiSceneSensors));

            if (gatedActionsThatShouldActivate != null)
            {
                await ExecuteFlexiScene(gatedActionsThatShouldActivate, InitiatedBy.Switch);
                SetTurnOffAt(gatedActionsThatShouldActivate);
            }
            else
            {
                _logger.LogWarning($"Click was detected but found no gated actions that could be executed.");
            }

            return;
        }

        if (InitiatedBy == InitiatedBy.Motion && InitialClickAfterMotionBehaviour == InitialClickAfterMotionBehaviour.ChangeOffDurationOnly)
        {
            var currentGatedActions = FlexiScenes.Single(x => x.Name == CurrentFlexiScene);
            InitiatedBy = InitiatedBy.Switch;
            SetTurnOffAt(currentGatedActions);
        }
        else
        {
            var currentGatedActionIndex = FlexiScenes.IndexOf(x => x.Name == CurrentFlexiScene);
            _flexiScenes.CurrentIndex = currentGatedActionIndex;

            var nextGatedActions = _flexiScenes.MoveNext;
            await ExecuteFlexiScene(nextGatedActions, InitiatedBy.Switch);

            SetTurnOffAt(nextGatedActions);
        }
    }

    private async Task Switch_DoubleClicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Double click triggered for switch {e.Sensor.EntityId}");

        if (!DoubleClickActions.Any())
            return;

        await ExecuteDoubleClickActions();
    }

    private async Task Switch_TripleClicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Triple click triggered for switch {e.Sensor.EntityId}");

        if (!TripleClickActions.Any())
            return;

        await ExecuteTripleClickActions();
    }
    private async Task Switch_LongClicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Long click triggered for switch {e.Sensor.EntityId}");

        if (!LongClickActions.Any())
            return;

        await ExecuteLongClickActions();
    }
    private async Task Switch_UberLongClicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Uber long click triggered for switch {e.Sensor.EntityId}");
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
    private async Task ExecuteFlexiSceneOnMotion(InitiatedBy initiatedBy)
    {
        if (CurrentFlexiScene != NONE)
        {
            RemoveTurnOffAt();
            return;
        }

        if (IlluminanceThreshold != null && IlluminanceSensors.All(x => x.State > IlluminanceThreshold))
        {
            _logger.LogDebug($"Motion sensor saw something moving but did not turn on lights because all illuminance sensors are above threshold of {IlluminanceThreshold}");
            return;
        }

        if (IgnorePresenceUntil != null && IgnorePresenceUntil > DateTime.Now)
        {
            _logger.LogDebug($"Motion sensor saw something moving but did not turn on lights because presence is ignored until {IgnorePresenceUntil:T}");
            return;
        }

        var flexiScenesThatShouldActivate = FlexiScenes.FirstOrDefault(x => x.CanActivate(FlexiSceneSensors));

        if (flexiScenesThatShouldActivate != null)
        {
            await ExecuteFlexiScene(flexiScenesThatShouldActivate, initiatedBy);
        }
        else
        {
            _logger.LogWarning($"Motion was detected but found no flexi scenes that could be executed.");
        }
    }

    private void MotionSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        var allMotionSensorsOff = MotionSensors.All(x => x.State == OFF);

        if (allMotionSensorsOff && CurrentFlexiScene != NONE)
        {
            var currentGatedActions = FlexiScenes.Single(x => x.Name == CurrentFlexiScene);
            SetTurnOffAt(currentGatedActions);
        }
    }

    private async Task Sensor_WentAboveThreshold(object? sender, NumericSensorEventArgs e)
    {
        if (AutoSwitchOffAboveIlluminance && CurrentFlexiScene != NONE)
        {
            _logger.LogDebug($"Executed off actions. Because a motion sensor exceeded the illuminance threshold ({e.New.State} lux)");
            await ExecuteOffActions();
        }
    }

    private async Task ExecuteFlexiSceneOnAutoTransition()
    {
        if (!AutoTransition)
            return;

        if (CurrentFlexiScene == NONE)
            return;

        var currentFlexiScene = FlexiScenes.Single(x => x.Name == CurrentFlexiScene);

        //current flexi scene still valid
        if (currentFlexiScene.CanActivate(FlexiSceneSensors))
            return;

        var flexiSceneThatShouldActivate = FlexiScenes.FirstOrDefault(x => x.CanActivate(FlexiSceneSensors));

        if (flexiSceneThatShouldActivate != null)
        {
            _logger.LogInformation($"Auto transition was triggered.");
            await ExecuteFlexiScene(flexiSceneThatShouldActivate, InitiatedBy, true);
        }
        else
        {
            //TODO: add setting to allow turning off
            _logger.LogInformation($"Auto transition was triggered but no flexi scene was found to transition to.");
        }
    }

    public async Task Guard(CancellationToken token)
    {
        while (true)
        {
            if (token.IsCancellationRequested)
                break;

            await AutoTurnOffIfNeeded();
            RemoveIgnorePresenceIfNeeded();
            await CheckForMotion();

            var delayTask = Task.Delay(1000, token);
            await delayTask;
        }
    }

    private void RemoveIgnorePresenceIfNeeded()
    {
        if (IgnorePresenceUntil != null && IgnorePresenceUntil < DateTime.Now)
            IgnorePresenceUntil = null;
    }

    private async Task AutoTurnOffIfNeeded()
    {
        if (TurnOffAt != null && TurnOffAt.Value < DateTime.Now)
        {
            await ExecuteOffActions();
            _logger.LogDebug($"Executed off actions.");
        }
    }
    private async Task CheckForMotion()
    {
        if (MotionSensors.All(x => x.IsOff()))
            return;

        if (CurrentFlexiScene != NONE)
            return;

        if (IgnorePresenceUntil != null && IgnorePresenceUntil > DateTime.Now)
            return;

        await ExecuteFlexiSceneOnMotion(InitiatedBy.Motion);
    }
}