using FlexiLights.Config;
using FlexiLights.Data.Rooms;
using FlexiLights.Data.Rooms.Evaluations;

namespace FlexiLights.Data.Helper;

internal static class EvaluationActionExtensions
{
    internal static List<IEvaluation> ConvertToDomainModel(this IList<ConditionConfig>? evaluations)
    {
        var evaluationList = new List<IEvaluation>();

        if (evaluations == null || !evaluations.Any())
            return evaluationList;

        foreach (var evaluationConfig in evaluations)
        {
            var action = evaluationConfig.ConvertToDomainModel();
            evaluationList.Add(action);
        }

        return evaluationList;
    }

    internal static IEvaluation ConvertToDomainModel(this ConditionConfig config)
    {
        return config switch
        {
            { Binary: not null } => config.ConvertToBinaryEvaluationDomainModel(),
            { And: not null } when config.And.Any() => config.ConvertToAndEvaluationDomainModel(),
            { Or: not null } when config.Or.Any() => config.ConvertToOrEvaluationDomainModel(),
            _ => throw new ArgumentException("invalid evaluation configuration")
        };
    }

    internal static IEvaluation ConvertToBinaryEvaluationDomainModel(this ConditionConfig config)
    {
        if (config.Binary == null)
            throw new ArgumentException("binary sensor not set");


        return config.BinaryMethod switch
        {
            BinaryMethod.True => new BinaryTrueEvaluation(config.Binary),
            BinaryMethod.False => new BinaryFalseEvaluation(config.Binary),
        };
    }

    internal static IEvaluation ConvertToAndEvaluationDomainModel(this ConditionConfig config)
    {
        if (config.And == null || !config.And.Any())
            throw new ArgumentException("Define at least one evaluation in your AND evaluation. 2 or more would make more sense though.");

        var evaluations = config.And.ConvertToDomainModel();
        return new AndEvaluation(evaluations);
    }

    internal static IEvaluation ConvertToOrEvaluationDomainModel(this ConditionConfig config)
    {
        if (config.Or == null || !config.Or.Any())
            throw new ArgumentException("Define at least one evaluation in your OR evaluation. 2 or more would make more sense though.");

        var evaluations = config.Or.ConvertToDomainModel();
        return new AndEvaluation(evaluations);
    }
}