using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace EvaluacionesApp.Tests;

public static class ReactiveUiTestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }
}
