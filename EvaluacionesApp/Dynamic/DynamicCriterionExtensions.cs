using System;
using DynamicData;

namespace EvaluacionesApp.Dynamic;

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
}
