using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Rooms.Evaluations;

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