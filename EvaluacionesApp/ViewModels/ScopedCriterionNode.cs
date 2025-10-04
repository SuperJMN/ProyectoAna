using System.Collections.Generic;
using System.Linq;
using EvaluacionesApp.Dynamic;

namespace EvaluacionesApp.ViewModels;

public class ScopedCriterionNode
{
    ScopedCriterionNode(DynamicCriterion criterion, IReadOnlyList<ScopedCriterionNode> children)
    {
        Criterion = criterion;
        Children = children;
    }

    public DynamicCriterion Criterion { get; }

    public string Id => Criterion.Id;

    public string Name => Criterion.Name;

    public double Weight => Criterion.Weight;

    public IReadOnlyList<ScopedCriterionNode> Children { get; }

    public bool IsLeaf => Children.Count == 0;

    public IEnumerable<ScopedCriterionNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }

    public static ScopedCriterionNode? Build(DynamicCriterion criterion, string? classId, int term)
    {
        if (!criterion.MatchesTreeScope(classId, term))
        {
            return null;
        }

        var children = criterion.FilterChildren(classId, term)
            .Select(child => Build(child, classId, term))
            .Where(child => child != null)
            .Select(child => child!)
            .ToList();

        return new ScopedCriterionNode(criterion, children);
    }
}
