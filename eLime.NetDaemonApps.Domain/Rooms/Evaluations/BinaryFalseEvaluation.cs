using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Rooms.Evaluations.Abstractions;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Rooms.Evaluations;

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