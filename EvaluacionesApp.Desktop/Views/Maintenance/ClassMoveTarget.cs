using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Views.Maintenance;

public sealed class ClassMoveTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private string displayName = string.Empty;
    private string courseName = string.Empty;
    private string className = string.Empty;
    private int courseOrder;

    public ClassMoveTarget(DynamicCourse course, DynamicClass @class)
    {
        Course = course;
        Class = @class;
        UpdateState();

        course.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.WhenAnyValue(x => x.Number)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        @class.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);
    }

    public DynamicCourse Course { get; }

    public DynamicClass Class { get; }

    public string Key => BuildKey(Course, Class);

    public string DisplayName => displayName;

    public string CourseName => courseName;

    public string ClassName => className;

    public int CourseOrder => courseOrder;

    public static string BuildKey(DynamicCourse course, DynamicClass @class)
    {
        return string.Create(course.Id.Length + @class.Id.Length + 1, (course.Id, @class.Id), static (span, state) =>
        {
            state.Item1.AsSpan().CopyTo(span);
            span[state.Item1.Length] = '/';
            state.Item2.AsSpan().CopyTo(span[(state.Item1.Length + 1)..]);
        });
    }

    void UpdateState()
    {
        var newCourseName = Course.Name;
        if (newCourseName != courseName)
        {
            courseName = newCourseName;
            this.RaisePropertyChanged(nameof(CourseName));
        }

        var newClassName = Class.Name;
        if (newClassName != className)
        {
            className = newClassName;
            this.RaisePropertyChanged(nameof(ClassName));
        }

        var newOrder = Course.Number ?? int.MaxValue;
        if (newOrder != courseOrder)
        {
            courseOrder = newOrder;
            this.RaisePropertyChanged(nameof(CourseOrder));
        }

        var newDisplayName = $"{newCourseName} / {newClassName}";
        if (newDisplayName != displayName)
        {
            displayName = newDisplayName;
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
