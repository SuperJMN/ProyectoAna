using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using EvaluacionesApp.Desktop.Persistence;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Dynamic;

public partial class DynamicCriterion : ReactiveObject, IDisposable
{
    private readonly DynamicCourse course;
    private readonly DynamicCriterion? parent;
    private readonly SourceCache<DynamicCriterion, string> childrenCache = new(child => child.Id);
    private readonly CompositeDisposable anchors = new();
    private readonly string id;

    [Reactive]
    private string name;

    [Reactive]
    private decimal weight;

    [Reactive]
    private string? classId = string.Empty;

    [Reactive]
    private int? term;

    public DynamicCriterion(Criterion model, DynamicCourse course, DynamicCriterion? parent)
    {
        this.course = course;
        this.parent = parent;
        id = model.Id;
        name = model.Name;
        weight = model.Weight;
        classId = string.IsNullOrWhiteSpace(model.ClassId) ? string.Empty : model.ClassId;
        term = model.Term;

        childrenCache.Connect()
            .Bind(out ReadOnlyObservableCollection<DynamicCriterion> children)
            .Subscribe()
            .DisposeWith(anchors);
        Children = children;

        childrenCache.Connect()
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsLeaf)))
            .DisposeWith(anchors);

        foreach (var child in model.Children)
        {
            var dynamicChild = new DynamicCriterion(child, course, this);
            childrenCache.AddOrUpdate(dynamicChild);
        }
    }

    public string Id => id;

    public bool IsLeaf => Children.Count == 0;

    internal DynamicCriterion? Parent => parent;

    public ReadOnlyObservableCollection<DynamicCriterion> Children { get; }

    public int? EffectiveTerm => Term ?? parent?.EffectiveTerm;

    public bool MatchesScope(int term)
    {
        return MatchesTerm(EffectiveTerm, term);
    }

    public bool MatchesTreeScope(int term)
    {
        if (MatchesScope(term))
        {
            return true;
        }

        return Children.Any(child => child.MatchesTreeScope(term));
    }

    public IEnumerable<DynamicCriterion> FilterChildren(int term)
    {
        return Children.Where(child => child.MatchesTreeScope(term));
    }

    public IObservable<IChangeSet<DynamicCriterion, string>> ChildrenChanges => childrenCache.Connect();

    public DynamicCriterion AddChild(Criterion model)
    {
        var child = new DynamicCriterion(model, course, this);
        childrenCache.AddOrUpdate(child);
        return child;
    }

    public void RemoveChild(DynamicCriterion child)
    {
        childrenCache.RemoveKey(child.Id);
        child.Dispose();
    }

    public Criterion ToDomain()
    {
        return new Criterion
        {
            Id = Id,
            Name = Name,
            Weight = Weight,
            ClassId = string.IsNullOrWhiteSpace(ClassId) ? string.Empty : ClassId,
            Term = Term,
            Children = Children.Select(c => c.ToDomain()).ToList()
        };
    }

    public void Dispose()
    {
        foreach (var child in Children.ToList())
        {
            child.Dispose();
        }

        anchors.Dispose();
    }

    static bool MatchesTerm(int? criterionTerm, int selectedTerm)
    {
        if (!criterionTerm.HasValue)
        {
            return selectedTerm == 1;
        }

        return criterionTerm.Value == selectedTerm;
    }
}
