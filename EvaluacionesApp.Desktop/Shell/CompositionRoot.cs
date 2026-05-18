using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.InitialSetup;
using EvaluacionesApp.Desktop.ViewModels;
using Serilog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Zafiro.UI.Shell;

namespace EvaluacionesApp.Desktop.Shell;

public static class CompositionRoot
{
    public static async Task<CompositionResult> CreateAsync()
    {
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        var services = new ServiceCollection();

        services.AddZafiroShell(logger: logger);
        services.AddAllSectionsFromAttributes(logger);

        var dialogService = DialogService.Create();
        services.AddSingleton(dialogService);
        services.AddSingleton<IDialog>(dialogService);

        services.AddSingleton<INotificationService>(new NotificationService(NotificationPosition.BottomRight));

        var persistenceService = new Services.PersistenceService();
        services.AddSingleton(persistenceService);

        var schoolModel = await persistenceService.Load();
        var root = new DynamicRoot(schoolModel);

        services.AddSingleton(root);
        services.AddSingleton<SchoolSelectionState>();
        services.AddSingleton<IDynamicSchoolStore>(sp =>
            new DynamicSchoolStore(sp.GetRequiredService<DynamicRoot>(), sp.GetRequiredService<Services.PersistenceService>()));
        services.AddSingleton<InitialSetupWizardFactory>();
        services.AddSingleton<InitialSetupApplicator>();
        services.AddSingleton<InitialSetupCoordinator>();

        var serviceProvider = services.BuildServiceProvider();
        var shell = serviceProvider.GetRequiredService<IShell>();
        return new CompositionResult(shell, serviceProvider);
    }

    public sealed record CompositionResult(IShell Shell, ServiceProvider Services);
}
