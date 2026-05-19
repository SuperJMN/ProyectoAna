using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.InitialSetup;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;
using Zafiro.UI.Shell;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

[Section(name: "Home", icon: "mdi-home-outline", sortIndex: 0, FriendlyName = "Inicio")]
public partial class GettingStartedViewModel : ReactiveObject, IGettingStartedViewModel, IDisposable
{
    private readonly InitialSetupCoordinator initialSetup;
    private readonly IShell shell;
    private readonly CompositeDisposable anchors = new();
    private readonly DynamicRoot root;
    private readonly Dictionary<DynamicCourse, CompositeDisposable> courseSubscriptions = new();
    private readonly Dictionary<DynamicClass, IDisposable> classSubscriptions = new();

    [Reactive(SetModifier = AccessModifier.Private)]
    private int courseCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int classCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int studentCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int criterionCount;

    public GettingStartedViewModel(
        IDynamicSchoolStore store,
        InitialSetupCoordinator initialSetup,
        IShell shell)
    {
        this.initialSetup = initialSetup;
        this.shell = shell;
        root = store.Root;

        RegisterExistingCourses();
        root.CoursesChanges
            .Subscribe(HandleCoursesChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(
                x => x.CourseCount,
                x => x.ClassCount,
                x => x.StudentCount,
                x => x.CriterionCount)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasCourses));
                this.RaisePropertyChanged(nameof(HasClasses));
                this.RaisePropertyChanged(nameof(HasStudents));
                this.RaisePropertyChanged(nameof(HasCriteria));
                this.RaisePropertyChanged(nameof(IsReadyForGrades));
                this.RaisePropertyChanged(nameof(CanStartInitialSetup));
                this.RaisePropertyChanged(nameof(SetupStatus));
                this.RaisePropertyChanged(nameof(NextSetupStepTitle));
                this.RaisePropertyChanged(nameof(NextSetupStepMessage));
                this.RaisePropertyChanged(nameof(NextSetupActionText));
            })
            .DisposeWith(anchors);

        var canStartInitialSetup = this.WhenAnyValue(x => x.CourseCount)
            .Select(count => count == 0)
            .DistinctUntilChanged();

        StartInitialSetup = ReactiveCommand
            .CreateFromTask(this.initialSetup.RunIfNeeded, canStartInitialSetup)
            .Enhance("Iniciar asistente");

        OpenNextSetupStep = ReactiveCommand.CreateFromTask(DoOpenNextSetupStep).Enhance("Continuar");

        RefreshSetupState();
    }

    public bool HasCourses => CourseCount > 0;

    public bool HasClasses => ClassCount > 0;

    public bool HasStudents => StudentCount > 0;

    public bool HasCriteria => CriterionCount > 0;

    public bool IsReadyForGrades => HasCourses && HasClasses && HasStudents && HasCriteria;

    public bool CanStartInitialSetup => CourseCount == 0;

    public string SetupStatus => IsReadyForGrades
        ? "Preparación lista para introducir notas."
        : CourseCount switch
    {
        0 => "Todavía no hay cursos configurados.",
        1 => "Configuración inicial lista: 1 curso.",
        _ => $"Configuración inicial lista: {CourseCount} cursos."
    };

    public string NextSetupStepTitle => ResolveNextStep().Title;

    public string NextSetupStepMessage => ResolveNextStep().Message;

    public string NextSetupActionText => ResolveNextStep().ActionText;

    public IEnhancedCommand<Unit> StartInitialSetup { get; }

    public IEnhancedCommand<Unit> OpenNextSetupStep { get; }

    async Task DoOpenNextSetupStep()
    {
        var next = ResolveNextStep();
        if (next.SectionId == null)
        {
            await initialSetup.RunIfNeeded();
            return;
        }

        shell.GoToSection(next.SectionId);
    }

    void RegisterExistingCourses()
    {
        foreach (var course in root.Courses)
        {
            RegisterCourse(course);
        }
    }

    void HandleCoursesChanged(IChangeSet<DynamicCourse, string> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    RegisterCourse(change.Current);
                    break;
                case ChangeReason.Remove:
                    UnregisterCourse(change.Current);
                    break;
            }
        }

        RefreshSetupState();
    }

    void RegisterCourse(DynamicCourse course)
    {
        if (courseSubscriptions.ContainsKey(course))
        {
            return;
        }

        var courseAnchors = new CompositeDisposable();
        course.ClassesChanges
            .Subscribe(HandleClassesChanged)
            .DisposeWith(courseAnchors);
        course.CriteriaChanges
            .MergeManyChangeSets(criterion => criterion.SelfAndDescendants())
            .Subscribe(_ => RefreshSetupState())
            .DisposeWith(courseAnchors);

        courseSubscriptions[course] = courseAnchors;
        foreach (var cls in course.Classes)
        {
            RegisterClass(cls);
        }
    }

    void UnregisterCourse(DynamicCourse course)
    {
        if (courseSubscriptions.Remove(course, out var subscription))
        {
            subscription.Dispose();
        }

        foreach (var cls in course.Classes)
        {
            UnregisterClass(cls);
        }
    }

    void HandleClassesChanged(IChangeSet<DynamicClass, string> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    RegisterClass(change.Current);
                    break;
                case ChangeReason.Remove:
                    UnregisterClass(change.Current);
                    break;
            }
        }

        RefreshSetupState();
    }

    void RegisterClass(DynamicClass cls)
    {
        if (classSubscriptions.ContainsKey(cls))
        {
            return;
        }

        classSubscriptions[cls] = cls.StudentsChanges
            .Subscribe(_ => RefreshSetupState());
    }

    void UnregisterClass(DynamicClass cls)
    {
        if (classSubscriptions.Remove(cls, out var subscription))
        {
            subscription.Dispose();
        }
    }

    void RefreshSetupState()
    {
        CourseCount = root.Courses.Count;
        ClassCount = root.Courses.Sum(course => course.Classes.Count);
        StudentCount = root.Courses.SelectMany(course => course.Classes).Sum(cls => cls.Students.Count);
        CriterionCount = root.Courses.Sum(course => course.EnumerateLeafCriteria().Count());
    }

    SetupStep ResolveNextStep()
    {
        if (!HasCourses)
        {
            return new SetupStep(
                "Crea la estructura inicial",
                "Empieza creando los cursos y clases de partida.",
                "Iniciar asistente",
                null);
        }

        if (!HasClasses)
        {
            return new SetupStep(
                "Añade clases",
                "Cada curso necesita al menos una clase antes de registrar alumnos.",
                "Abrir clases",
                "Classes");
        }

        if (!HasStudents)
        {
            return new SetupStep(
                "Añade alumnos",
                "Registra los alumnos de las clases antes de introducir notas.",
                "Abrir alumnos",
                "Students");
        }

        if (!HasCriteria)
        {
            return new SetupStep(
                "Define criterios",
                "Crea los criterios del trimestre para que las notas tengan estructura.",
                "Abrir criterios",
                "Criteria");
        }

        return new SetupStep(
            "Preparado para evaluar",
            "Ya puedes introducir y revisar las notas.",
            "Abrir notas",
            "Grades");
    }

    public void Dispose()
    {
        foreach (var subscription in courseSubscriptions.Values)
        {
            subscription.Dispose();
        }

        courseSubscriptions.Clear();
        foreach (var subscription in classSubscriptions.Values)
        {
            subscription.Dispose();
        }

        classSubscriptions.Clear();
        anchors.Dispose();
    }

    readonly record struct SetupStep(string Title, string Message, string ActionText, string? SectionId);
}
