using eLime.netDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Conditions;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.NumericSensors;
using eLime.NetDaemonApps.Domain.Rooms.Actions;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = eLime.NetDaemonApps.Domain.Rooms.Actions.Action;
using Switch = eLime.NetDaemonApps.Domain.BinarySensors.Switch;

namespace eLime.NetDaemonApps.Domain.Rooms;

public class Room
{
    public string? Name { get; }
    public bool AutoTransition { get; }
    private readonly DebounceDispatcher AutoTransitionDebounceDispatcher = new(TimeSpan.FromSeconds(1));

    public InitialClickAfterMotionBehaviour InitialClickAfterMotionBehaviour { get; }
    public Int32? IlluminanceThreshold { get; }
    public bool AutoSwitchOffAboveIlluminance { get; }
    public TimeSpan IgnorePresenceAfterOffDuration { get; }
    private DateTime? TurnOffAt { get; set; }
    private DateTime? IgnorePresenceUntil { get; set; }
    private InitiatedBy InitiatedBy { get; set; } = InitiatedBy.NoOne;

    private readonly List<BinarySensor> _offSensors = new();
    public IReadOnlyCollection<BinarySensor> OffSensors => _offSensors.AsReadOnly();

    public void AddOffSensor(BinarySensor sensor)
    {
        sensor.TurnedOn += async (s, e) => await OffSensor_TurnedOn(s, e);
        _offSensors.Add(sensor);
    }

    //private readonly List<Light> _lights = new();
    //public IReadOnlyCollection<Light> Lights => _lights.AsReadOnly();
    //public void AddLight(Light light) => _lights.Add(light);

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

    public FlexiScenes FlexiScenes { get; }
    public FlexiScene? FlexiSceneThatShouldActivate => FlexiScenes.GetSceneThatShouldActivate(FlexiSceneSensors);


    private readonly IHaContext _haContext;
    private readonly ILogger _logger;

    public Room(IHaContext haContext, ILogger logger, RoomConfig config)
    {
        _haContext = haContext;
        _logger = logger;

        Name = config.Name;
        AutoTransition = config.AutoTransition;
        InitialClickAfterMotionBehaviour = config.InitialClickAfterMotionBehaviour == eLime.netDaemonApps.Config.FlexiLights.InitialClickAfterMotionBehaviour.ChangeOffDurationOnly ? InitialClickAfterMotionBehaviour.ChangeOffDurationOnly : InitialClickAfterMotionBehaviour.ChangeOFfDurationAndGoToNextAutomation;
        IlluminanceThreshold = config.IlluminanceThreshold;
        AutoSwitchOffAboveIlluminance = config.AutoSwitchOffAboveIlluminance;
        IgnorePresenceAfterOffDuration = config.IgnorePresenceAfterOffDuration ?? TimeSpan.Zero;


        //if (config.Lights == null || !config.Lights.Any())
        //    throw new Exception("Define at least one light");

        //foreach (var lightId in config.Lights)
        //{
        //    if (Lights.Any(x => x.EntityId == lightId))
        //        continue;

        //    var light = Light.Create(_haContext, lightId);
        //    AddLight(light);
        //}


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

        if (config.Switches != null && config.Switches.Any())
        {
            foreach (var switchId in config.Switches)
            {
                if (Switches.Any(x => x.EntityId == switchId))
                    continue;

                var sensor = Switch.Create(_haContext, switchId, config.ClickInterval, config.LongClickDuration, config.UberLongClickDuration);
                AddSwitch(sensor);
            }
        }

        if (!Switches.Any() && !MotionSensors.Any())
            throw new Exception("Define at least one switch or motion sensor");

        if (config.OffActions == null || !config.OffActions.Any())
            throw new Exception("Define at least one off action");

        if (config.OffActions.Any(x => x.ExecuteOffActions))
            throw new ArgumentException("Do not define ExecuteOFfActions within OFF actions. That would cause a circular depedency.");

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
                logger.LogDebug($"No evaluations were found on flexi scene '{flexiScene.Name}'. This will always resolve to true. Intended or configuration problem?");
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
    }

    private async Task ExecuteFlexiScene(FlexiScene flexiScene, InitiatedBy initiatedBy, Boolean autoTransition = false)
    {
        _logger.LogInformation($"Will execute flexi scene {flexiScene.Name}. Triggered by {initiatedBy}.");
        FlexiScenes.SetCurrentScene(flexiScene);
        InitiatedBy = initiatedBy;

        await ExecuteActions(flexiScene.Actions, autoTransition);
    }
    private async Task ExecuteOffActions()
    {
        FlexiScenes.DeactivateScene();
        InitiatedBy = InitiatedBy.NoOne;
        IgnorePresenceUntil = DateTime.Now.Add(IgnorePresenceAfterOffDuration);
        await ExecuteActions(OffActions);
        ClearAutoTurnOff();
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

    private void SetTurnOffAt(FlexiScene flexiScene)
    {
        var timeSpan = InitiatedBy switch
        {
            InitiatedBy.Motion => flexiScene.TurnOffAfterIfTriggeredByMotionSensor,
            InitiatedBy.Switch => flexiScene.TurnOffAfterIfTriggeredBySwitch,
            _ => throw new ArgumentOutOfRangeException()
        };

        TurnOffAt = DateTime.Now.Add(timeSpan);
        _logger.LogDebug($"Off actions will be executed at {TurnOffAt:T}. Turn off at was set by {InitiatedBy}");
    }
    private void ClearAutoTurnOff()
    {
        _logger.LogDebug($"Off actions will no longer be executed. Probably because the OFF actions were just executed or a motion sensor is active.");
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
        //If motion / presence is detected and there is an active scene do nothing but cancel auto turn off
        if (FlexiScenes.Current != null)
        {
            ClearAutoTurnOff();
            return;
        }

        await ExecuteFlexiSceneOnMotion();
    }

    private async Task Switch_Clicked(object? sender, BinarySensorEventArgs e)
    {
        _logger.LogDebug($"Click triggered for switch {e.Sensor.EntityId}");
        if (FlexiScenes.Current == null)
        {
            if (FlexiSceneThatShouldActivate != null)
            {
                await ExecuteFlexiScene(FlexiSceneThatShouldActivate, InitiatedBy.Switch);
                SetTurnOffAt(FlexiSceneThatShouldActivate);
            }
            else
            {
                _logger.LogWarning($"Click was detected but found no flexi scene that could be executed.");
            }

            return;
        }

        if (InitiatedBy == InitiatedBy.Motion && InitialClickAfterMotionBehaviour == InitialClickAfterMotionBehaviour.ChangeOffDurationOnly)
        {
            InitiatedBy = InitiatedBy.Switch;
            SetTurnOffAt(FlexiScenes.Current);
        }
        else
        {
            var nextFlexiScene = FlexiScenes.Next;
            await ExecuteFlexiScene(nextFlexiScene, InitiatedBy.Switch);

            SetTurnOffAt(nextFlexiScene);
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
    private async Task ExecuteFlexiSceneOnMotion()
    {
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

        if (FlexiSceneThatShouldActivate != null)
        {
            await ExecuteFlexiScene(FlexiSceneThatShouldActivate, InitiatedBy.Motion);
        }
        else
        {
            _logger.LogWarning($"Motion was detected but found no flexi scenes that could be executed.");
        }
    }

    private void MotionSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        var allMotionSensorsOff = MotionSensors.All(x => x.IsOff());

        if (allMotionSensorsOff && FlexiScenes.Current != null)
        {
            SetTurnOffAt(FlexiScenes.Current);
        }
    }

    private async Task Sensor_WentAboveThreshold(object? sender, NumericSensorEventArgs e)
    {
        if (AutoSwitchOffAboveIlluminance && FlexiScenes.Current != null)
        {
            _logger.LogDebug($"Executed off actions. Because a motion sensor exceeded the illuminance threshold ({e.New.State} lux)");
            await ExecuteOffActions();
        }
    }

    private async Task ExecuteFlexiSceneOnAutoTransition()
    {
        if (!AutoTransition)
            return;

        if (FlexiScenes.Current == null)
            return;

        //current flexi scene still valid
        if (FlexiScenes.Current.CanActivate(FlexiSceneSensors))
            return;

        if (FlexiSceneThatShouldActivate != null)
        {
            _logger.LogInformation($"Auto transition was triggered.");
            await ExecuteFlexiScene(FlexiSceneThatShouldActivate, InitiatedBy, true);
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
            //Can only happen executes on startup, otherwise we receive a motion detected event
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

        if (FlexiScenes.Current != null)
            return;

        if (IgnorePresenceUntil != null && IgnorePresenceUntil > DateTime.Now)
            return;

        await ExecuteFlexiSceneOnMotion();
    }
}