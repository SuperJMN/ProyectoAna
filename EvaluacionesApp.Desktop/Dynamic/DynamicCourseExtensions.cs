using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using DynamicData.Binding;

namespace EvaluacionesApp.Desktop.Dynamic;

public static class DynamicCourseExtensions
{
    public static IObservable<IChangeSet<DynamicCriterion, string>> AllCriteria(this DynamicCourse course)
    {
        return course.CriteriaChanges.MergeManyChangeSets(criterion => criterion.SelfAndDescendants());
    }

    public static IObservable<IChangeSet<DynamicCriterion, string>> LeafCriteria(this DynamicCourse course)
    {
        return course.CriteriaChanges
            .MergeManyChangeSets(criterion => criterion.SelfAndDescendants())
            .AutoRefresh(c => c.IsLeaf)
            .AutoRefresh(c => c.Term)
            .Filter(c => c.IsLeaf);
    }

    public static IEnumerable<DynamicCriterion> EnumerateLeafCriteria(this DynamicCourse course)
    {
        ArgumentNullException.ThrowIfNull(course);

        return course.Criteria
            .SelectMany(criterion => criterion.EnumerateSelfAndDescendants())
            .Where(criterion => criterion.IsLeaf);
    }

    public static IEnumerable<DynamicCriterion> EnumerateLeafCriteria(this DynamicCourse course, int term)
    {
        ArgumentNullException.ThrowIfNull(course);

        var result = new List<DynamicCriterion>();
        foreach (var criterion in course.Criteria)
        {
            CollectLeaves(criterion, term, result);
        }

        return result;
    }

    public static IEnumerable<DynamicCriterion> FilterCriteriaTree(this DynamicCourse course, int term)
    {
        ArgumentNullException.ThrowIfNull(course);

        foreach (var criterion in course.Criteria)
        {
            if (criterion.MatchesTreeScope(term))
            {
                yield return criterion;
            }
        }
    }

    static void CollectLeaves(
        DynamicCriterion node,
        int term,
        ICollection<DynamicCriterion> result)
    {
        if (!node.MatchesTreeScope(term))
        {
            return;
        }

        if (node.Children.Count == 0)
        {
            if (node.MatchesScope(term))
            {
                result.Add(node);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            CollectLeaves(child, term, result);
        }
    }
}
