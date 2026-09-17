using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.Classes;

[Section(name: "Classes", icon: "mdi-human-male-board", sortIndex: 3, FriendlyName = "Clases")]
public partial class ClassesViewModel : ReactiveValidationObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());

    private readonly IDynamicSchoolStore store;
    private readonly INotificationService notifications;
    private readonly CompositeDisposable anchors = new();
    private CompositeDisposable? courseAnchors;
    private DynamicRoot? root;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;

    public ReactiveCommand<Unit, Unit> AddClass { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public ClassesViewModel(IDynamicSchoolStore store, INotificationService notifications)
    {
        this.store = store;
        this.notifications = notifications;
        var canAdd = this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null);
        AddClass = ReactiveCommand.CreateFromTask(DoAddClass, canAdd);
        this.ValidationRule(ObserveSelectedClassName(), "El nombre de la clase no puede estar vacio");
        Save = ReactiveCommand.CreateFromTask(ExecuteSave, ValidationContext.Valid);
        Save.ThrownExceptions
            .Subscribe(ex => _ = this.notifications.Show("No se pudieron guardar las clases", ex.Message))
            .DisposeWith(anchors);
        _ = Load();
    }

    async Task Load()
    {
        root = store.Root;
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(_ => RaiseClassStateChanged())
            .DisposeWith(anchors);

        root.CoursesChanges
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RaiseClassStateChanged())
            .DisposeWith(anchors);

        RaiseClassStateChanged();
        await Task.CompletedTask;
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;

        if (course == null)
        {
            SelectedClass = null;
            RaiseClassStateChanged();
            return;
        }

        courseAnchors = new CompositeDisposable();

        course.ClassesChanges
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Id)
            .Where(changes => changes.Any(change => change.Reason == ChangeReason.Refresh))
            .Throttle(TimeSpan.FromMilliseconds(400), RxSchedulers.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(courseAnchors);

        course.ClassesChanges
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (SelectedClass != null && !course.Classes.Contains(SelectedClass))
                {
                    SelectedClass = course.Classes.FirstOrDefault();
                }

                RaiseClassStateChanged();
            })
            .DisposeWith(courseAnchors);

        SelectedClass = course.Classes.FirstOrDefault();
        RaiseClassStateChanged();
    }

    async Task DoAddClass()
    {
        if (SelectedCourse == null)
        {
            return;
        }

        var idx = SelectedCourse.Classes.Count + 1;
        var model = new Class { Id = $"{SelectedCourse.Id}-class-{idx}", Name = $"Clase {idx}" };
        var cls = SelectedCourse.AddClass(model);
        SelectedClass = cls;
        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    IObservable<bool> ObserveSelectedClassName()
    {
        return this.WhenAnyValue(x => x.SelectedClass)
            .Select(cls => cls?.WhenAnyValue(x => x.Name).Select(HasText) ?? Observable.Return(true))
            .Switch()
            .DistinctUntilChanged();
    }

    public bool HasCourses => Courses.Count > 0;

    public bool HasSelectedCourse => SelectedCourse != null;

    public bool HasClassesForSelectedCourse => SelectedCourse?.Classes.Count > 0;

    public bool HasSelectedClass => SelectedClass != null;

    public bool ShowNoCoursesState => !HasCourses;

    public bool ShowClassesEmptyState => HasSelectedCourse && !HasClassesForSelectedCourse;

    public bool ShowClassList => HasClassesForSelectedCourse;

    public bool ShowClassDetails => HasSelectedClass;

    public bool ShowToolbarAddClassAction => HasClassesForSelectedCourse;

    void RaiseClassStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasCourses));
        this.RaisePropertyChanged(nameof(HasSelectedCourse));
        this.RaisePropertyChanged(nameof(HasClassesForSelectedCourse));
        this.RaisePropertyChanged(nameof(HasSelectedClass));
        this.RaisePropertyChanged(nameof(ShowNoCoursesState));
        this.RaisePropertyChanged(nameof(ShowClassesEmptyState));
        this.RaisePropertyChanged(nameof(ShowClassList));
        this.RaisePropertyChanged(nameof(ShowClassDetails));
        this.RaisePropertyChanged(nameof(ShowToolbarAddClassAction));
    }

    static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public new void Dispose()
    {
        courseAnchors?.Dispose();
        anchors.Dispose();
        base.Dispose();
    }
}
