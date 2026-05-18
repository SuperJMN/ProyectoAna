using System.Reactive;
using ReactiveUI;
using Zafiro.UI.Commands;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

public sealed class GettingStartedViewModelSample : IGettingStartedViewModel
{
    public int CourseCount => 0;

    public bool HasCourses => false;

    public bool CanStartInitialSetup => true;

    public string SetupStatus => "Todavía no hay cursos configurados.";

    public IEnhancedCommand<Unit> StartInitialSetup { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Iniciar asistente");

    public IEnhancedCommand<Unit> OpenCourses { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir cursos");

    public IEnhancedCommand<Unit> OpenClasses { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir clases");

    public IEnhancedCommand<Unit> OpenCriteria { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir criterios");
}
