using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.Criteria;

public sealed class CourseCriterionCopyTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly ObservableCollection<TermCriterionCopyTarget> termsInternal = new();
    private readonly ReadOnlyObservableCollection<TermCriterionCopyTarget> terms;
    private string courseName = string.Empty;
    private int courseOrder;

    public CourseCriterionCopyTarget(DynamicCourse course)
    {
        Course = course;
        terms = new ReadOnlyObservableCollection<TermCriterionCopyTarget>(termsInternal);

        UpdateState();
        UpdateTerms();

        course.WhenAnyValue(x => x.Name)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.WhenAnyValue(x => x.Number)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.TermsChanges
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateTerms())
            .DisposeWith(anchors);
    }

    public DynamicCourse Course { get; }

    public string CourseId => Course.Id;

    public string CourseName => courseName;

    public int CourseOrder => courseOrder;

    public ReadOnlyObservableCollection<TermCriterionCopyTarget> Terms => terms;

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

    void UpdateTerms()
    {
        termsInternal.Clear();

        var termValues = Course.Terms.Count > 0
            ? Course.Terms.ToList()
            : new List<int> { 1, 2, 3 };

        foreach (var term in termValues.Distinct().OrderBy(x => x))
        {
            termsInternal.Add(new TermCriterionCopyTarget(new CriterionCopyTarget(Course, term)));
        }
    }

    public void Dispose()
    {
        anchors.Dispose();
        termsInternal.Clear();
    }
}
