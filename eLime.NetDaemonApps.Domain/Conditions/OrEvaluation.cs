using eLime.NetDaemonApps.Domain.Conditions.Abstractions;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Conditions;

public class OrCondition : ICondition
{
    public IReadOnlyCollection<ICondition> Conditions { get; init; }
    public OrCondition(IReadOnlyCollection<ICondition> conditions)
    {
        Conditions = conditions;
    }

    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        return Conditions.Any(x => x.Evaluate(sensors));
    }

    public IReadOnlyCollection<(string, ConditionSensorType)> GetSensorsIds()
    {
        return Conditions.SelectMany(x => x.GetSensorsIds()).ToList();
    }
}