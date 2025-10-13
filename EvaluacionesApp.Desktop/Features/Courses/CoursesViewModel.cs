using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Features.Courses;

public partial class CoursesViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private DynamicRoot? root;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    [Reactive] private DynamicCourse? selectedCourse;

    public ReactiveCommand<Unit, Unit> AddCourse { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public CoursesViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        AddCourse = ReactiveCommand.CreateFromTask(DoAddCourse);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        _ = Load();
    }

    async Task Load()
    {
        root = store.Root;
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();

        root.CoursesChanges
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Id)
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(anchors);
        await Task.CompletedTask;
    }

    async Task DoAddCourse()
    {
        var targetRoot = store.Root;
        var idx = targetRoot.Courses.Count + 1;
var course = targetRoot.AddCourse(new Course { Id = $"course-{idx}", Name = $"Course {idx}" });
        SelectedCourse = course;
        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
