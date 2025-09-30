using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicCourse : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicClass, string> classesCache = new(c => c.Id);
    private readonly SourceCache<DynamicCriterion, string> criteriaCache = new(c => c.Id);
    private readonly CompositeDisposable anchors = new();
    private readonly Dictionary<DynamicClass, IDisposable> classSubscriptions = new();
    private readonly Dictionary<DynamicCriterion, IDisposable> criterionSubscriptions = new();

    private string id;
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
            RegisterClass(dynamicClass);
            classesCache.AddOrUpdate(dynamicClass);
        }

        foreach (var criterion in model.Criteria)
        {
            var dynamicCriterion = new DynamicCriterion(criterion, this, null);
            RegisterCriterion(dynamicCriterion);
            criteriaCache.AddOrUpdate(dynamicCriterion);
        }
    }

    public string Id
    {
        get => id;
        set
        {
            if (value == id)
            {
                return;
            }
            var previous = id;
            this.RaiseAndSetIfChanged(ref id, value);
            IdChanged?.Invoke(this, previous);
        }
    }

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public ReadOnlyObservableCollection<DynamicClass> Classes { get; }

    public ReadOnlyObservableCollection<DynamicCriterion> Criteria { get; }

    public IObservable<IChangeSet<DynamicClass, string>> ClassesChanges => classesCache.Connect();

    public IObservable<IChangeSet<DynamicCriterion, string>> CriteriaChanges => criteriaCache.Connect();

    public event Action<DynamicCourse, string>? IdChanged;

    public DynamicClass AddClass(Class model)
    {
        var cls = new DynamicClass(model, this);
        RegisterClass(cls);
        classesCache.AddOrUpdate(cls);
        return cls;
    }

    public void RemoveClass(DynamicClass cls)
    {
        if (classSubscriptions.Remove(cls, out var disposer))
        {
            disposer.Dispose();
        }
        classesCache.RemoveKey(cls.Id);
        cls.Dispose();
    }

    public DynamicCriterion AddCriterion(Criterion model)
    {
        var criterion = new DynamicCriterion(model, this, null);
        RegisterCriterion(criterion);
        criteriaCache.AddOrUpdate(criterion);
        return criterion;
    }

    public void RemoveCriterion(DynamicCriterion criterion)
    {
        if (criterionSubscriptions.Remove(criterion, out var disposer))
        {
            disposer.Dispose();
        }
        criteriaCache.RemoveKey(criterion.Id);
        criterion.Dispose();
    }

    internal void OnClassIdChanged(DynamicClass cls, string previousId)
    {
        classesCache.Edit(cache =>
        {
            cache.RemoveKey(previousId);
            cache.AddOrUpdate(cls);
        });
    }

    internal void OnCriterionIdChanged(DynamicCriterion criterion, string previousId)
    {
        criteriaCache.Edit(cache =>
        {
            cache.RemoveKey(previousId);
            cache.AddOrUpdate(criterion);
        });
        foreach (var cls in Classes)
        {
            cls.UpdateAssessmentsForCriterion(previousId, criterion.Id);
        }
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

    private void RegisterClass(DynamicClass cls)
    {
        void Handler(DynamicClass sender, string previous)
        {
            OnClassIdChanged(sender, previous);
        }

        cls.IdChanged += Handler;
        classSubscriptions[cls] = Disposable.Create(() => cls.IdChanged -= Handler);
    }

    private void RegisterCriterion(DynamicCriterion criterion)
    {
        void Handler(DynamicCriterion sender, string previous)
        {
            OnCriterionIdChanged(sender, previous);
        }

        criterion.IdChanged += Handler;
        criterionSubscriptions[criterion] = Disposable.Create(() => criterion.IdChanged -= Handler);
    }

    public void Dispose()
    {
        foreach (var subscription in classSubscriptions.Values)
        {
            subscription.Dispose();
        }
        classSubscriptions.Clear();

        foreach (var subscription in criterionSubscriptions.Values)
        {
            subscription.Dispose();
        }
        criterionSubscriptions.Clear();

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
