using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using EvaluacionesApp.Desktop;
using ReactiveUI.Avalonia;

namespace EvaluacionesApp.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI(_ => { })
            .LogToTrace();
    }
}
