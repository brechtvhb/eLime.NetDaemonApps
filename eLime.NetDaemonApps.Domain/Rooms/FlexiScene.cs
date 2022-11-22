using eLime.netDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Rooms.Evaluations;
using eLime.NetDaemonApps.Domain.Rooms.Evaluations.Abstractions;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = eLime.NetDaemonApps.Domain.Rooms.Actions.Action;

namespace eLime.NetDaemonApps.Domain.Rooms;

public class FlexiScene
{
    public String Name { get; private init; }

    private List<IEvaluation> _evaluations = new();
    public IReadOnlyCollection<IEvaluation> Evaluations => _evaluations.AsReadOnly();

    private List<Action> _actions = new();
    public IReadOnlyCollection<Action> Actions => _actions.AsReadOnly();

    public TimeSpan TurnOffAfterIfTriggeredBySwitch { get; private init; }
    public TimeSpan TurnOffAfterIfTriggeredByMotionSensor { get; private init; }

    public static FlexiScene Create(IHaContext haContext, FlexiSceneConfig config)
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