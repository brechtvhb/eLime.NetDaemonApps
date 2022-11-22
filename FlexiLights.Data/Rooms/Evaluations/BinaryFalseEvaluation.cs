using FlexiLights.Data.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Rooms.Evaluations;

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