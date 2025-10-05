using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Views.Maintenance;

public sealed class CourseMoveTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<ClassMoveTarget, string> classesCache = new(target => target.Key);
    private readonly ReadOnlyObservableCollection<ClassMoveTarget> classes;
    private string courseName = string.Empty;
    private int courseOrder;

    public CourseMoveTarget(DynamicCourse course)
    {
        Course = course;

        classesCache.Connect()
            .OnItemRemoved(DisposeClassTarget)
            .AutoRefresh(target => target.ClassName)
            .Sort(SortExpressionComparer<ClassMoveTarget>
                .Ascending(target => target.ClassName))
            .Bind(out classes)
            .Subscribe()
            .DisposeWith(anchors);

        UpdateState();

        course.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.WhenAnyValue(x => x.Number)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);
    }

    public DynamicCourse Course { get; }

    public string CourseId => Course.Id;

    public string CourseName => courseName;

    public int CourseOrder => courseOrder;

    public ReadOnlyObservableCollection<ClassMoveTarget> Classes => classes;

    public bool IsEmpty => classes.Count == 0;

    public void AddOrUpdateClass(DynamicClass cls)
    {
        var key = ClassMoveTarget.BuildKey(Course, cls);
        var existing = classesCache.Lookup(key);
        if (existing.HasValue)
        {
            if (!ReferenceEquals(existing.Value.Class, cls))
            {
                classesCache.RemoveKey(key);
                classesCache.AddOrUpdate(new ClassMoveTarget(Course, cls));
            }

            return;
        }

        classesCache.AddOrUpdate(new ClassMoveTarget(Course, cls));
    }

    public void RemoveClass(DynamicClass cls)
    {
        classesCache.RemoveKey(ClassMoveTarget.BuildKey(Course, cls));
    }

    void DisposeClassTarget(ClassMoveTarget target)
    {
        target.Dispose();
    }

    void UpdateState()
    {
        var newName = Course.Name;
        if (newName != courseName)
        {
            courseName = newName;
            this.RaisePropertyChanged(nameof(CourseName));
        }

        var newOrder = Course.Number ?? int.MaxValue;
        if (newOrder != courseOrder)
        {
            courseOrder = newOrder;
            this.RaisePropertyChanged(nameof(CourseOrder));
        }
    }

    public void Dispose()
    {
        anchors.Dispose();

        foreach (var target in classesCache.Items.ToList())
        {
            target.Dispose();
        }

        classesCache.Dispose();
    }
}
