using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Views.Maintenance;

public sealed class ClassCriterionCopyTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly ReadOnlyObservableCollection<TermCriterionCopyTarget> terms;
    private readonly ObservableCollection<TermCriterionCopyTarget> termsInternal = new();
    private string className = string.Empty;

    public ClassCriterionCopyTarget(DynamicCourse course, DynamicClass @class)
    {
        Course = course;
        Class = @class;

        foreach (var term in new[] { 1, 2, 3 })
        {
            termsInternal.Add(new TermCriterionCopyTarget(new CriterionCopyTarget(course, @class, term)));
        }

        terms = new ReadOnlyObservableCollection<TermCriterionCopyTarget>(termsInternal);

        UpdateState();

        @class.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);
    }

    public DynamicCourse Course { get; }

    public DynamicClass Class { get; }

    public string Key => ClassMoveTarget.BuildKey(Course, Class);

    public string ClassName => className;

    public ReadOnlyObservableCollection<TermCriterionCopyTarget> Terms => terms;

    void UpdateState()
    {
        var newName = Class.Name;
        if (newName == className)
        {
            return;
        }

        className = newName;
        this.RaisePropertyChanged(nameof(ClassName));
    }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
