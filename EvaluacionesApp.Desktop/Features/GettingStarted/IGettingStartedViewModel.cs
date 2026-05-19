using System.Reactive;
using Zafiro.UI.Commands;

namespace EvaluacionesApp.Desktop.Features.GettingStarted;

public interface IGettingStartedViewModel
{
    int CourseCount { get; }
    int ClassCount { get; }
    int StudentCount { get; }
    int CriterionCount { get; }
    bool HasCourses { get; }
    bool HasClasses { get; }
    bool HasStudents { get; }
    bool HasCriteria { get; }
    bool IsReadyForGrades { get; }
    bool CanStartInitialSetup { get; }
    string SetupStatus { get; }
    string NextSetupStepTitle { get; }
    string NextSetupStepMessage { get; }
    string NextSetupActionText { get; }
    IEnhancedCommand<Unit> StartInitialSetup { get; }
    IEnhancedCommand<Unit> OpenNextSetupStep { get; }
}
