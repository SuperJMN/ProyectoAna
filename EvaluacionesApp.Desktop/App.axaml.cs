using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading;
using Avalonia.Markup.Xaml;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Shell;
using EvaluacionesApp.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Controls.Shell;

namespace EvaluacionesApp.Desktop;

public partial class App : Application
{
    private ServiceProvider? services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            this.Connect(
                () => new ShellView(),
                _ =>
                {
                    var result = CompositionRoot.CreateAsync().GetAwaiter().GetResult();
                    services = result.Services;
                    return result.Shell;
                },
                () => new MainWindow());

            desktop.ShutdownRequested += OnShutdownRequested;
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        var store = services?.GetService<IDynamicSchoolStore>();
        if (store == null) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            store.SaveAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[App] Save on shutdown failed: {ex}");
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        services?.Dispose();
        services = null;
    }
}
