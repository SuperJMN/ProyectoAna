using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using DynamicData.Binding;

namespace EvaluacionesApp.Dynamic;

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
            .Filter(c => c.IsLeaf);
    }

    public static IEnumerable<DynamicCriterion> EnumerateLeafCriteria(this DynamicCourse course)
    {
        ArgumentNullException.ThrowIfNull(course);

        return course.Criteria
            .SelectMany(criterion => criterion.EnumerateSelfAndDescendants())
            .Where(criterion => criterion.IsLeaf);
    }
}
