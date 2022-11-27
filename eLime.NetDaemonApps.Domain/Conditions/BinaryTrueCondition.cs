using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Conditions.Abstractions;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Conditions;

public class BinaryTrueCondition : ICondition
{
    public string SensorId { get; init; }

    public BinaryTrueCondition(string sensorId)
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

    public IReadOnlyCollection<(string, ConditionSensorType)> GetSensorsIds()
    {
        return new List<(string, ConditionSensorType)> { (SensorId, ConditionSensorType.Binary) };
    }
}