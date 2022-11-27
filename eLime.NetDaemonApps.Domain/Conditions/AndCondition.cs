using eLime.NetDaemonApps.Domain.Conditions.Abstractions;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Conditions;

public class AndCondition : ICondition
{
    public IReadOnlyCollection<ICondition> Conditions { get; init; }
    public AndCondition(IReadOnlyCollection<ICondition> evaluations)
    {
        Conditions = evaluations;
    }

    public bool Evaluate(IReadOnlyCollection<Entity> sensors)
    {
        return Conditions.All(x => x.Evaluate(sensors));
    }

    public IReadOnlyCollection<(string, ConditionSensorType)> GetSensorsIds()
    {
        return Conditions.SelectMany(x => x.GetSensorsIds()).ToList();
    }
}