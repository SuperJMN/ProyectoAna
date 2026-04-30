using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Persistence;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Dynamic;

public partial class DynamicCourse : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicClass, string> classesCache = new(c => c.Id);
    private readonly SourceCache<DynamicCriterion, string> criteriaCache = new(c => c.Id);
    private readonly SourceCache<int, int> termsCache = new(term => term);
    private readonly CompositeDisposable anchors = new();

    private readonly string id;

    [Reactive]
    private string name;

    [Reactive]
    private int? number;

    public DynamicCourse(Course model, DynamicRoot _)
    {
        id = model.Id;
        name = model.Name;
        number = model.Number;

        classesCache.Connect()
            .Bind(out ReadOnlyObservableCollection<DynamicClass> classes)
            .Subscribe()
            .DisposeWith(anchors);
        Classes = classes;

        criteriaCache.Connect()
            .Bind(out ReadOnlyObservableCollection<DynamicCriterion> criteria)
            .Subscribe()
            .DisposeWith(anchors);
        Criteria = criteria;

        termsCache.Connect()
            .Sort(SortExpressionComparer<int>.Ascending(x => x))
            .Bind(out ReadOnlyObservableCollection<int> terms)
            .Subscribe()
            .DisposeWith(anchors);
        Terms = terms;

        foreach (var cls in model.Classes)
        {
            var dynamicClass = new DynamicClass(cls, this);
            classesCache.AddOrUpdate(dynamicClass);
        }

        foreach (var criterion in model.Criteria)
        {
            var dynamicCriterion = new DynamicCriterion(criterion, this, null);
            criteriaCache.AddOrUpdate(dynamicCriterion);
        }

        var termSource = model.Terms.Count > 0 ? model.Terms : DeriveTerms(model.Criteria);
        foreach (var term in termSource)
        {
            termsCache.AddOrUpdate(term);
        }
    }

    public string Id => id;

    public ReadOnlyObservableCollection<DynamicClass> Classes { get; }

    public ReadOnlyObservableCollection<DynamicCriterion> Criteria { get; }

    public ReadOnlyObservableCollection<int> Terms { get; }

    public IObservable<IChangeSet<DynamicClass, string>> ClassesChanges => classesCache.Connect();

    public IObservable<IChangeSet<DynamicCriterion, string>> CriteriaChanges => criteriaCache.Connect();

    public IObservable<IChangeSet<int, int>> TermsChanges => termsCache.Connect();

    public DynamicClass AddClass(Class model)
    {
        var cls = new DynamicClass(model, this);
        classesCache.AddOrUpdate(cls);
        return cls;
    }

    public void RemoveClass(DynamicClass cls)
    {
        classesCache.RemoveKey(cls.Id);
        cls.Dispose();
    }

    public DynamicCriterion AddCriterion(Criterion model)
    {
        var criterion = new DynamicCriterion(model, this, null);
        criteriaCache.AddOrUpdate(criterion);
        return criterion;
    }

    public void RemoveCriterion(DynamicCriterion criterion)
    {
        criteriaCache.RemoveKey(criterion.Id);
        criterion.Dispose();
    }

    public Course ToDomain()
    {
        return new Course
        {
            Id = Id,
            Name = Name,
            Number = Number,
            Classes = Classes.Select(c => c.ToDomain()).ToList(),
            Criteria = Criteria.Select(c => c.ToDomain()).ToList(),
            Terms = Terms.Distinct().OrderBy(x => x).ToList()
        };
    }

    public void Dispose()
    {
        foreach (var cls in Classes.ToList())
        {
            cls.Dispose();
        }

        foreach (var criterion in Criteria.ToList())
        {
            criterion.Dispose();
        }

        anchors.Dispose();
    }

    static IEnumerable<int> DeriveTerms(IEnumerable<Criterion> criteria)
    {
        var set = new HashSet<int>();

        void Walk(IEnumerable<Criterion> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Term.HasValue)
                {
                    set.Add(node.Term.Value);
                }

                if (node.Children.Count > 0)
                {
                    Walk(node.Children);
                }
            }
        }

        Walk(criteria);
        set.Add(1);
        return set.Count > 0 ? set : new[] { 1, 2, 3 };
    }
}
