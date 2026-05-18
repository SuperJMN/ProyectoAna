using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public partial class InitialSetupCourseOptionViewModel : ReactiveObject
{
    [Reactive] private bool isSelected;

    public InitialSetupCourseOptionViewModel(string name, int? number)
    {
        Name = name;
        Number = number;
    }

    public string Name { get; }

    public int? Number { get; }

    public InitialSetupCourseDraft CreateDraft()
    {
        return new InitialSetupCourseDraft(Name, Number);
    }
}
