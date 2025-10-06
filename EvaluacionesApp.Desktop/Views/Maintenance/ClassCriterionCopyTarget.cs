using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
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

        terms = new ReadOnlyObservableCollection<TermCriterionCopyTarget>(termsInternal);

        UpdateTerms();

        course.TermsChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateTerms())
            .DisposeWith(anchors);

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

    void UpdateTerms()
    {
        termsInternal.Clear();

        IEnumerable<int> termValues = Course.Terms.Count > 0 ? Course.Terms : new[] { 1, 2, 3 };

        foreach (var term in termValues)
        {
            termsInternal.Add(new TermCriterionCopyTarget(new CriterionCopyTarget(Course, Class, term)));
        }
    }
}
