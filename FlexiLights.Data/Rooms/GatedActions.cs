using FlexiLights.Config;
using FlexiLights.Data.BinarySensors;
using FlexiLights.Data.Helper;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Action = FlexiLights.Data.Rooms.Actions.Action;

namespace FlexiLights.Data.Rooms;

public class GatedActions
{
    public String Name { get; private init; }

    private List<IEvaluation> _evaluations = new();
    public IReadOnlyCollection<IEvaluation> Evaluations => _evaluations.AsReadOnly();

    private List<Action> _actions = new();
    public IReadOnlyCollection<Action> Actions => _actions.AsReadOnly();

    public TimeSpan TurnOffAfterIfTriggeredBySwitch { get; private init; }
    public TimeSpan TurnOffAfterIfTriggeredByMotionSensor { get; private init; }

    public static GatedActions Create(IHaContext haContext, GatedActionConfig config)
    {
        if (String.IsNullOrWhiteSpace(config.Name))
            throw new ArgumentException("gated actions must have a name");

        var gatedAction = new GatedActions
        {
            Name = config.Name,
            _evaluations = config.Evaluations.ConvertToDomainModel(),
            _actions = config.Actions.ConvertToDomainModel(haContext),
            TurnOffAfterIfTriggeredByMotionSensor = config.TurnOffAfterIfTriggeredByMotionSensor ?? TimeSpan.FromMinutes(5),
            TurnOffAfterIfTriggeredBySwitch = config.TurnOffAfterIfTriggeredBySwitch ?? TimeSpan.FromDays(1)
        };

        return gatedAction;
    }

    public bool CanActivate(IReadOnlyCollection<Entity> gatedActionSensors)
    {
        return Evaluations.All(x => x.Evaluate(gatedActionSensors));
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return Evaluations
            .SelectMany(x => x.GetSensorsIds())
            .Distinct()
            .ToList();
    }
}

public interface IEvaluation
{
    public bool Evaluate(IReadOnlyCollection<Entity> sensors);

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds();
}

public class OrEvaluation : IEvaluation
{
    public IReadOnlyCollection<IEvaluation> Evaluations { get; init; }
    public OrEvaluation(IReadOnlyCollection<IEvaluation> evaluations)
    {
        Evaluations = evaluations;
    }

    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        return Evaluations.Any(x => x.Evaluate(sensors));
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return Evaluations.SelectMany(x => x.GetSensorsIds()).ToList();
    }
}
public class AndEvaluation : IEvaluation
{
    public IReadOnlyCollection<IEvaluation> Evaluations { get; init; }
    public AndEvaluation(IReadOnlyCollection<IEvaluation> evaluations)
    {
        Evaluations = evaluations;
    }

    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        return Evaluations.All(x => x.Evaluate(sensors));
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return Evaluations.SelectMany(x => x.GetSensorsIds()).ToList();
    }
}

public class BinaryTrueEvaluation : IEvaluation
{
    public string SensorId { get; init; }

    public BinaryTrueEvaluation(string sensorId)
    {
        SensorId = sensorId;
    }

    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        var sensor = sensors.Single(x => x.EntityId == SensorId);

        if (sensor is not BinarySensor)
            return false;

        return sensor.State == "on";
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return new List<(string, EvaluationSensorType)> { (SensorId, EvaluationSensorType.Binary) };
    }
}

public class BinaryFalseEvaluation : IEvaluation
{
    public string SensorId { get; init; }

    public BinaryFalseEvaluation(string sensorId)
    {
        SensorId = sensorId;
    }
    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        var sensor = sensors.Single(x => x.EntityId == SensorId);

        if (sensor is not BinarySensor)
            return false;

        return sensor.State != "on";
    }

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds()
    {
        return new List<(string, EvaluationSensorType)> { (SensorId, EvaluationSensorType.Binary) };
    }
}

public enum EvaluationSensorType
{
    Binary
}