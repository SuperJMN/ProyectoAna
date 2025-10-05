using System;
using System.Collections.Generic;
using DynamicData;

namespace EvaluacionesApp.Desktop.Dynamic;

public static class DynamicCriterionExtensions
{
    public static IObservable<IChangeSet<DynamicCriterion, string>> AllDescendants(this DynamicCriterion criterion)
    {
        return criterion.ChildrenChanges.MergeManyChangeSets(child => child.AllDescendants());
    }

    public static IObservable<IChangeSet<DynamicCriterion, string>> SelfAndDescendants(this DynamicCriterion criterion)
    {
        return criterion.ChildrenChanges.MergeChangeSets(
            criterion.ChildrenChanges.MergeManyChangeSets(child => child.SelfAndDescendants()));
    }

    public static IEnumerable<DynamicCriterion> EnumerateSelfAndDescendants(this DynamicCriterion criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);

        yield return criterion;

        foreach (var child in criterion.Children)
        {
            foreach (var descendant in child.EnumerateSelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}
