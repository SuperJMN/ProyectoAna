using System;
using System.Collections.Generic;
using System.Linq;
using EvaluacionesApp.Desktop.ViewModels;

namespace EvaluacionesApp.Desktop.Views.Grades;

public static class CriterionScoreAggregator
{
    public static decimal? Compute(ScopedCriterionNode node, ScoreRow row)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(row);

        var selfScore = row.GetScore(node.Criterion.Id);
        var childrenScore = AggregateChildren(node, row);

        if (selfScore.HasValue && childrenScore.HasValue)
        {
            return selfScore.Value + childrenScore.Value;
        }

        if (selfScore.HasValue)
        {
            return selfScore.Value;
        }

        return childrenScore;
    }

    static decimal? AggregateChildren(ScopedCriterionNode node, ScoreRow row)
    {
        if (node.Children.Count == 0)
        {
            return null;
        }

        var contributions = new List<(decimal Value, decimal Weight)>();

        foreach (var child in node.Children)
        {
            var childScore = Compute(child, row);
            if (!childScore.HasValue)
            {
                continue;
            }

            var weight = Math.Max(child.Criterion.Weight, 0m);
            contributions.Add((childScore.Value, weight));
        }

        if (contributions.Count == 0)
        {
            return null;
        }

        var weightSum = contributions.Sum(entry => entry.Weight);
        if (weightSum <= 0m)
        {
            return contributions.Average(entry => entry.Value);
        }

        var total = contributions.Sum(entry => entry.Value * (entry.Weight / weightSum));
        return total;
    }
}
