using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Conditions;
using eLime.NetDaemonApps.Domain.Conditions.Abstractions;
using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

public class FlexiScene
{
    public string Name { get; private init; }
    public bool Secondary { get; private init; }
    private List<ICondition> _conditions = [];
    public IReadOnlyCollection<ICondition> Conditions => _conditions.AsReadOnly();
    private List<ICondition> _fullyAutomatedConditions = [];
    public IReadOnlyCollection<ICondition> FullyAutomatedConditions => _fullyAutomatedConditions.AsReadOnly();

    private List<Action> _actions = [];
    public IReadOnlyCollection<Action> Actions => _actions.AsReadOnly();

    public TimeSpan TurnOffAfterIfTriggeredBySwitch { get; private init; }
    public TimeSpan TurnOffAfterIfTriggeredByMotionSensor { get; private init; }

    private List<string> _nextFlexiScenes = new();
    public IReadOnlyCollection<string> NextFlexiScenes => _nextFlexiScenes.AsReadOnly();

    public static FlexiScene Create(IHaContext haContext, FlexiSceneConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new ArgumentException("flexi scene must have a name");

        var flexiScene = new FlexiScene
        {
            Name = config.Name,
            Secondary = config.Secondary,
            _conditions = config.Conditions.ConvertToDomainModel(),
            _fullyAutomatedConditions = config.FullyAutomatedConditions.ConvertToDomainModel(),
            _actions = config.Actions.ConvertToDomainModel(haContext),
            TurnOffAfterIfTriggeredByMotionSensor = config.TurnOffAfterIfTriggeredByMotionSensor ?? TimeSpan.FromMinutes(5),
            TurnOffAfterIfTriggeredBySwitch = config.TurnOffAfterIfTriggeredBySwitch ?? TimeSpan.FromHours(8),
            _nextFlexiScenes = config.NextFlexiScenes?.ToList() ?? []
        };

        return flexiScene;
    }

    public bool CanActivate(IReadOnlyCollection<Entity> flexiSceneSensors)
    {
        return Conditions.All(x => x.Evaluate(flexiSceneSensors));
    }
    public bool CanActivateFullyAutomated(IReadOnlyCollection<Entity> flexiSceneSensors)
    {
        return FullyAutomatedConditions.All(x => x.Evaluate(flexiSceneSensors));
    }

    public IReadOnlyCollection<(string, ConditionSensorType)> GetSensorsIds()
    {
        var mergedConditionSensors = new List<(string, ConditionSensorType)>();
        mergedConditionSensors.AddRange(Conditions.SelectMany(x => x.GetSensorsIds()));
        mergedConditionSensors.AddRange(FullyAutomatedConditions.SelectMany(x => x.GetSensorsIds()));

        return mergedConditionSensors.Distinct().ToList();
    }
}