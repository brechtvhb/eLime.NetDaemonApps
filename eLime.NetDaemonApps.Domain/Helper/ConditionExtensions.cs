using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Conditions;
using eLime.NetDaemonApps.Domain.Conditions.Abstractions;

namespace eLime.NetDaemonApps.Domain.Helper;

internal static class ConditionExtensions
{
    internal static List<ICondition> ConvertToDomainModel(this IList<ConditionConfig>? conditions)
    {
        var conditionList = new List<ICondition>();

        if (conditions == null || !conditions.Any())
            return conditionList;

        foreach (var evaluationConfig in conditions)
        {
            var action = evaluationConfig.ConvertToDomainModel();
            conditionList.Add(action);
        }

        return conditionList;
    }

    internal static ICondition ConvertToDomainModel(this ConditionConfig config)
    {
        return config switch
        {
            { Binary: not null } => config.ConvertToBinaryConditionDomainModel(),
            { And: not null } when config.And.Any() => config.ConvertToAndConditionDomainModel(),
            { Or: not null } when config.Or.Any() => config.ConvertToOrConditionDomainModel(),
            _ => throw new ArgumentException("invalid evaluation configuration")
        };
    }

    internal static ICondition ConvertToBinaryConditionDomainModel(this ConditionConfig config)
    {
        return config.Binary switch
        {
            null => throw new ArgumentException("binary sensor not set"),
            { } when config.Binary.StartsWith("!") => new BinaryFalseCondition(config.Binary[1..]),
            _ => new BinaryTrueCondition(config.Binary),
        };
    }

    internal static ICondition ConvertToAndConditionDomainModel(this ConditionConfig config)
    {
        if (config.And == null || !config.And.Any())
            throw new ArgumentException("Define at least one evaluation in your AND evaluation. 2 or more would make more sense though.");

        var evaluations = config.And.ConvertToDomainModel();
        return new AndCondition(evaluations);
    }

    internal static ICondition ConvertToOrConditionDomainModel(this ConditionConfig config)
    {
        if (config.Or == null || !config.Or.Any())
            throw new ArgumentException("Define at least one evaluation in your OR evaluation. 2 or more would make more sense though.");

        var evaluations = config.Or.ConvertToDomainModel();
        return new OrCondition(evaluations);
    }
}