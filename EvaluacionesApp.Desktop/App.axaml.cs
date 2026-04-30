using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Shell;
using EvaluacionesApp.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Misc;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia.MaterialDesign;

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
            DisableAvaloniaDataAnnotationValidation();
            IconProvider.Current
                .Register<FontAwesomeIconProvider>()
                .Register<MaterialDesignIconProvider>();

            this.Connect(
                () => new MainView(),
                async _ =>
                {
                    var result = await CompositionRoot.CreateAsync();
                    services = result.Services;
                    return result.ViewModel;
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
        if (store == null)
        {
            return;
        }

        try
        {
            // Block briefly so any throttled in-flight changes are flushed before the
            // process tears down. We avoid Wait()-ing forever to keep the user able to
            // close the app even if the disk is wedged.
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

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
