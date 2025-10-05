using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using EvaluacionesApp.Desktop.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Dynamic;

public partial class DynamicCourse : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicClass, string> classesCache = new(c => c.Id);
    private readonly SourceCache<DynamicCriterion, string> criteriaCache = new(c => c.Id);
    private readonly CompositeDisposable anchors = new();

    private readonly string id;

    [Reactive]
    private string name;

    public DynamicCourse(Course model, DynamicRoot _)
    {
        id = model.Id;
        name = model.Name;

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
    }

    public string Id => id;

    public ReadOnlyObservableCollection<DynamicClass> Classes { get; }

    public ReadOnlyObservableCollection<DynamicCriterion> Criteria { get; }

    public IObservable<IChangeSet<DynamicClass, string>> ClassesChanges => classesCache.Connect();

    public IObservable<IChangeSet<DynamicCriterion, string>> CriteriaChanges => criteriaCache.Connect();

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
            Classes = Classes.Select(c => c.ToDomain()).ToList(),
            Criteria = Criteria.Select(c => c.ToDomain()).ToList()
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
}
