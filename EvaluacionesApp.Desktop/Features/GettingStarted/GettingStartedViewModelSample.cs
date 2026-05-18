using System.Reactive;
using ReactiveUI;
using Zafiro.UI.Commands;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

public sealed class GettingStartedViewModelSample : IGettingStartedViewModel
{
    public int CourseCount => 0;

    public int ClassCount => 0;

    public int StudentCount => 0;

    public int CriterionCount => 0;

    public bool HasCourses => false;

    public bool HasClasses => false;

    public bool HasStudents => false;

    public bool HasCriteria => false;

    public bool IsReadyForGrades => false;

    public bool CanStartInitialSetup => true;

    public string SetupStatus => "Todavía no hay cursos configurados.";

    public string NextSetupStepTitle => "Crea la estructura inicial";

    public string NextSetupStepMessage => "Empieza con el asistente para crear cursos y clases.";

    public string NextSetupActionText => "Iniciar asistente";

    public IEnhancedCommand<Unit> StartInitialSetup { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Iniciar asistente");

    public IEnhancedCommand<Unit> OpenNextSetupStep { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Continuar");

    public IEnhancedCommand<Unit> OpenCourses { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir cursos");

    public IEnhancedCommand<Unit> OpenClasses { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir clases");

    public IEnhancedCommand<Unit> OpenStudents { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir alumnos");

    public IEnhancedCommand<Unit> OpenCriteria { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir criterios");

    public IEnhancedCommand<Unit> OpenGrades { get; } =
        ReactiveCommand.Create(() => { }).Enhance("Abrir notas");
}
