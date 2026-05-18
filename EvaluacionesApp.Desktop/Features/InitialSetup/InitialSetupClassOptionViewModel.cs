using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public partial class InitialSetupClassOptionViewModel : ReactiveObject
{
    [Reactive] private bool isSelected;

    public InitialSetupClassOptionViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
