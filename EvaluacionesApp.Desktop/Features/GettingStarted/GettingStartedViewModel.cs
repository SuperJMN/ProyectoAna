using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.InitialSetup;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;
using Zafiro.UI.Shell;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

[Section(name: "Home", icon: "mdi-home", sortIndex: 0, FriendlyName = "Inicio")]
public partial class GettingStartedViewModel : ReactiveObject, IGettingStartedViewModel, IDisposable
{
    private readonly InitialSetupCoordinator initialSetup;
    private readonly IShell shell;
    private readonly CompositeDisposable anchors = new();

    [Reactive(SetModifier = AccessModifier.Private)]
    private int courseCount;

    public GettingStartedViewModel(
        IDynamicSchoolStore store,
        InitialSetupCoordinator initialSetup,
        IShell shell)
    {
        this.initialSetup = initialSetup;
        this.shell = shell;

        store.Root.CoursesChanges
            .ToCollection()
            .Select(courses => courses.Count)
            .StartWith(store.Root.Courses.Count)
            .DistinctUntilChanged()
            .Subscribe(count => CourseCount = count)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.CourseCount)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasCourses));
                this.RaisePropertyChanged(nameof(CanStartInitialSetup));
                this.RaisePropertyChanged(nameof(SetupStatus));
            })
            .DisposeWith(anchors);

        var canStartInitialSetup = this.WhenAnyValue(x => x.CourseCount)
            .Select(count => count == 0)
            .DistinctUntilChanged();

        StartInitialSetup = ReactiveCommand
            .CreateFromTask(this.initialSetup.RunIfNeeded, canStartInitialSetup)
            .Enhance("Iniciar asistente");

        OpenCourses = ReactiveCommand.Create(() => this.shell.GoToSection("Courses")).Enhance("Abrir cursos");
        OpenClasses = ReactiveCommand.Create(() => this.shell.GoToSection("Classes")).Enhance("Abrir clases");
        OpenCriteria = ReactiveCommand.Create(() => this.shell.GoToSection("Criteria")).Enhance("Abrir criterios");
    }

    public bool HasCourses => CourseCount > 0;

    public bool CanStartInitialSetup => CourseCount == 0;

    public string SetupStatus => CourseCount switch
    {
        0 => "Todavía no hay cursos configurados.",
        1 => "Configuración inicial lista: 1 curso.",
        _ => $"Configuración inicial lista: {CourseCount} cursos."
    };

    public IEnhancedCommand<Unit> StartInitialSetup { get; }

    public IEnhancedCommand<Unit> OpenCourses { get; }

    public IEnhancedCommand<Unit> OpenClasses { get; }

    public IEnhancedCommand<Unit> OpenCriteria { get; }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
