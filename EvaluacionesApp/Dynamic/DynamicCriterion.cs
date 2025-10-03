using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicCriterion : ReactiveObject, IDisposable
{
    private readonly DynamicCourse course;
    private readonly DynamicCriterion? parent;
    private readonly SourceCache<DynamicCriterion, string> childrenCache = new(child => child.Id);
    private readonly CompositeDisposable anchors = new();
    private readonly string id;
    private string name;
    private double weight;

    public DynamicCriterion(Criterion model, DynamicCourse course, DynamicCriterion? parent)
    {
        this.course = course;
        this.parent = parent;
        id = model.Id;
        name = model.Name;
        weight = model.Weight;

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

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public double Weight
    {
        get => weight;
        set => this.RaiseAndSetIfChanged(ref weight, value);
    }

    public bool IsLeaf => Children.Count == 0;

    internal DynamicCriterion? Parent => parent;

    public ReadOnlyObservableCollection<DynamicCriterion> Children { get; }

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
}
