using FlexiLights.Data.Helper;
using FlexiLights.Data.Rooms.Evaluations;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = FlexiLights.Data.Rooms.Actions.Action;

namespace FlexiLights.Data.Rooms;

public class FlexiScene
{
    public String Name { get; private init; }

    private List<IEvaluation> _evaluations = new();
    public IReadOnlyCollection<IEvaluation> Evaluations => _evaluations.AsReadOnly();

    private List<Action> _actions = new();
    public IReadOnlyCollection<Action> Actions => _actions.AsReadOnly();

    public TimeSpan TurnOffAfterIfTriggeredBySwitch { get; private init; }
    public TimeSpan TurnOffAfterIfTriggeredByMotionSensor { get; private init; }

    public static FlexiScene Create(IHaContext haContext, Config.FlexiSceneConfig config)
    {
        if (String.IsNullOrWhiteSpace(config.Name))
            throw new ArgumentException("gated actions must have a name");

        var flexiScene = new FlexiScene
        {
            Name = config.Name,
            _evaluations = config.Conditions.ConvertToDomainModel(),
            _actions = config.Actions.ConvertToDomainModel(haContext),
            TurnOffAfterIfTriggeredByMotionSensor = config.TurnOffAfterIfTriggeredByMotionSensor ?? TimeSpan.FromMinutes(5),
            TurnOffAfterIfTriggeredBySwitch = config.TurnOffAfterIfTriggeredBySwitch ?? TimeSpan.FromDays(1)
        };

        return flexiScene;
    }

    public bool CanActivate(IReadOnlyCollection<Entity> flexiSceneSensors)
    {
        return Evaluations.All(x => x.Evaluate(flexiSceneSensors));
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return Evaluations
            .SelectMany(x => x.GetSensorsIds())
            .Distinct()
            .ToList();
    }
}