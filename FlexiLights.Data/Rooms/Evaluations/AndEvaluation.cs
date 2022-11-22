using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Rooms.Evaluations;

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