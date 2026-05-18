using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.Courses;

[Section(name: "Courses", icon: "mdi-school", sortIndex: 2, FriendlyName = "Cursos")]
public partial class CoursesViewModel : ReactiveValidationObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());

    private readonly IDynamicSchoolStore store;
    private readonly INotificationService notifications;
    private readonly CompositeDisposable anchors = new();
    private DynamicRoot? root;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    [Reactive] private DynamicCourse? selectedCourse;

    public ReactiveCommand<Unit, Unit> AddCourse { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public CoursesViewModel(IDynamicSchoolStore store, INotificationService notifications)
    {
        this.store = store;
        this.notifications = notifications;
        AddCourse = ReactiveCommand.CreateFromTask(DoAddCourse);
        this.ValidationRule(ObserveSelectedCourseName(), "El nombre del curso no puede estar vacio");
        Save = ReactiveCommand.CreateFromTask(ExecuteSave, ValidationContext.Valid);
        Save.ThrownExceptions
            .Subscribe(ex => _ = this.notifications.Show("No se pudieron guardar los cursos", ex.Message))
            .DisposeWith(anchors);
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
            .Throttle(TimeSpan.FromMilliseconds(400), RxSchedulers.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(anchors);

        root.CoursesChanges
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RaiseCourseStateChanged())
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(_ => RaiseCourseStateChanged())
            .DisposeWith(anchors);

        RaiseCourseStateChanged();
        await Task.CompletedTask;
    }

    async Task DoAddCourse()
    {
        var targetRoot = store.Root;
        var idx = targetRoot.Courses.Count + 1;
        var course = targetRoot.AddCourse(new Course { Id = $"course-{idx}", Name = $"Curso {idx}" });
        SelectedCourse = course;
        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    IObservable<bool> ObserveSelectedCourseName()
    {
        return this.WhenAnyValue(x => x.SelectedCourse)
            .Select(course => course?.WhenAnyValue(x => x.Name).Select(HasText) ?? Observable.Return(true))
            .Switch()
            .DistinctUntilChanged();
    }

    public bool HasCourses => Courses.Count > 0;

    public bool HasSelectedCourse => SelectedCourse != null;

    public bool ShowCoursesEmptyState => !HasCourses;

    public bool ShowCourseList => HasCourses;

    public bool ShowCourseDetails => HasSelectedCourse;

    public bool ShowToolbarAddCourseAction => HasCourses;

    void RaiseCourseStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasCourses));
        this.RaisePropertyChanged(nameof(HasSelectedCourse));
        this.RaisePropertyChanged(nameof(ShowCoursesEmptyState));
        this.RaisePropertyChanged(nameof(ShowCourseList));
        this.RaisePropertyChanged(nameof(ShowCourseDetails));
        this.RaisePropertyChanged(nameof(ShowToolbarAddCourseAction));
    }

    static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public new void Dispose()
    {
        anchors.Dispose();
        base.Dispose();
    }
}
