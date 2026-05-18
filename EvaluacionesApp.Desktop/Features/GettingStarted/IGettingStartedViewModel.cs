using System.Reactive;
using Zafiro.UI.Commands;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

public interface IGettingStartedViewModel
{
    int CourseCount { get; }
    bool HasCourses { get; }
    bool CanStartInitialSetup { get; }
    string SetupStatus { get; }
    IEnhancedCommand<Unit> StartInitialSetup { get; }
    IEnhancedCommand<Unit> OpenCourses { get; }
    IEnhancedCommand<Unit> OpenClasses { get; }
    IEnhancedCommand<Unit> OpenCriteria { get; }
}
