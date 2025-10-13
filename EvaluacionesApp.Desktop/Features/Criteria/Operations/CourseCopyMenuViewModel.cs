using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.ViewModels;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.Criteria.Operations;

public sealed class CourseCopyMenuViewModel : MenuViewModel, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private int courseOrder;

    public CourseCopyMenuViewModel(CourseCriterionCopyTarget target, ReactiveCommand<CriterionCopyTarget?, Unit> command)
    {
        Key = target.CourseId;
        Header = target.CourseName;
        CourseOrder = target.CourseOrder;

        target.WhenAnyValue(x => x.CourseName)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(value => Header = value)
            .DisposeWith(anchors);

        target.WhenAnyValue(x => x.CourseOrder)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(value => CourseOrder = value)
            .DisposeWith(anchors);

        target.Terms
            .ToObservableChangeSet()
            .Select(changes => changes.Transform(term => (MenuItemViewModel)new TermCopyMenuItemViewModel(term, command)))
            .DisposeMany()
            .Bind(out var termItems)
            .Subscribe()
            .DisposeWith(anchors);

        SetChildren(termItems);
    }

    public int CourseOrder
    {
        get => courseOrder;
        private set => this.RaiseAndSetIfChanged(ref courseOrder, value);
    }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
