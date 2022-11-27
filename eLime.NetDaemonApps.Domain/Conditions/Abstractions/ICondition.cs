using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Conditions.Abstractions;

public interface ICondition
{
    public bool Evaluate(IReadOnlyCollection<Entity> sensors);

    public IReadOnlyCollection<(string, ConditionSensorType)> GetSensorsIds();
}