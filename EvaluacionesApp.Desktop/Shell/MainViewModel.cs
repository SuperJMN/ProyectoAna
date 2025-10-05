using Zafiro.UI.Shell;

namespace EvaluacionesApp.Desktop.Shell;

public class MainViewModel(IShell shell)
{
    public IShell Shell { get; set; } = shell;
}
