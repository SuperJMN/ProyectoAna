using System;
using System.Linq;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Reactive.Linq;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell;
using EvaluacionesApp.Dynamic;

namespace EvaluacionesApp.Shell;

public static class CompositionRoot
{
    public static MainViewModel Create()
    {
        ServiceCollection services = new();

        // Use the real Shell implementation which depends on ShellProperties and registered sections
        services.AddSingleton<IShell, Zafiro.UI.Shell.Shell>();
        services.AddSingleton(new ShellProperties("Evaluaciones", navigatorObj => CreateHeaderFromNavigator(navigatorObj)));
        services.AddSingleton(DialogService.Create());

        // Defer NotificationService initialization until TopLevel is available (Loaded)
        var topLevel = ApplicationUtils.TopLevel().GetValueOrThrow("TopLevel not ready for NotificationService");
        var notificationManager = new WindowNotificationManager(topLevel) { Position = NotificationPosition.BottomRight };
        services.AddSingleton<INotificationService>(new NotificationService(notificationManager));

        // Application services
        services.AddSingleton<Services.PersistenceService>();
        services.AddSingleton<DynamicSchoolStore>();

        // VMs
        services.AddTransient<Views.Grades.GradesViewModel>();
        services.AddTransient<Views.Maintenance.CoursesViewModel>();
        services.AddTransient<Views.Maintenance.ClassesViewModel>();
        services.AddTransient<Views.Maintenance.StudentsViewModel>();
        services.AddTransient<Views.Maintenance.CriteriaViewModel>();

        // Sections registration
        AddSections(services);

        services.AddTransient<MainViewModel>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<MainViewModel>();
    }

    private static void AddSections(ServiceCollection services)
    {
        // Register sections using Zafiro's navigation builder
        AddNavigation.RegisterSections(services, builder =>
        {
            builder.Add<Views.Grades.GradesViewModel>("Notas", new Icon { Source = "mdi-numeric-3-box-multiple" }, true);
            builder.Add<Views.Maintenance.CoursesViewModel>("Cursos", new Icon { Source = "mdi-school" }, true);
            builder.Add<Views.Maintenance.ClassesViewModel>("Clases", new Icon { Source = "mdi-google-classroom" }, true);
            builder.Add<Views.Maintenance.StudentsViewModel>("Alumnos", new Icon { Source = "mdi-account-group" }, true);
            builder.Add<Views.Maintenance.CriteriaViewModel>("Criterios", new Icon { Source = "mdi-format-list-bulleted" }, true);
        }, logger: null, scheduler: RxApp.MainThreadScheduler);
    }

    private static IObservable<object?> CreateHeaderFromNavigator(object navigatorObj)
    {
        var navigator = (INavigator)navigatorObj;
        return navigator.Content.Select(o =>
        {
            var type = o?.GetType();
            return type?.Name.Replace("ViewModel", "") ?? "Sección";
        });
    }
}
