using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Rooms.Evaluations;

public interface IEvaluation
{
    public bool Evaluate(IReadOnlyCollection<Entity> sensors);

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds();
}