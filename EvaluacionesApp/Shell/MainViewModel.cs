using Zafiro.UI.Shell;

namespace EvaluacionesApp.Shell;

public class MainViewModel(IShell shell)
{
    public IShell Shell { get; set; } = shell;
}
