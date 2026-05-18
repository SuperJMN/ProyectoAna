using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Wizards.Graph.Core;
using Zafiro.UI;
using Zafiro.UI.Shell;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupCoordinator
{
    private readonly IDynamicSchoolStore store;
    private readonly IDialog dialog;
    private readonly InitialSetupWizardFactory wizardFactory;
    private readonly InitialSetupApplicator applicator;
    private readonly IShell shell;
    private readonly INotificationService notifications;
    private bool started;

    public InitialSetupCoordinator(
        IDynamicSchoolStore store,
        IDialog dialog,
        InitialSetupWizardFactory wizardFactory,
        InitialSetupApplicator applicator,
        IShell shell,
        INotificationService notifications)
    {
        this.store = store;
        this.dialog = dialog;
        this.wizardFactory = wizardFactory;
        this.applicator = applicator;
        this.shell = shell;
        this.notifications = notifications;
    }

    public async Task RunIfNeeded()
    {
        if (started || store.Root.Courses.Count > 0)
        {
            return;
        }

        started = true;

        try
        {
            var result = await ShowWizard(wizardFactory.Create());
            if (result.HasNoValue)
            {
                return;
            }

            await applicator.Apply(result.Value);
            shell.GoToSection("Classes");
        }
        catch (Exception ex)
        {
            await notifications.Show("No se pudo completar el asistente inicial", ex.Message);
        }
    }

    async Task<Maybe<InitialSetupDraft>> ShowWizard(GraphWizard<InitialSetupDraft> wizard)
    {
        var result = Maybe<InitialSetupDraft>.None;
        using var resultSubscription = wizard.Finished.Subscribe(value => result = Maybe.From(value));
        IDisposable? closeSubscription = null;

        try
        {
            var completed = await dialog.Show(
                wizard,
                CurrentTitle(wizard),
                (shownWizard, closeable) =>
                {
                    closeSubscription = shownWizard.Finish.Take(1).Subscribe(_ => closeable.Close());
                    return Array.Empty<IOption>();
                },
                size: DialogSize.Wide);

            return completed ? result : Maybe<InitialSetupDraft>.None;
        }
        finally
        {
            closeSubscription?.Dispose();
        }
    }

    static IObservable<string> CurrentTitle(GraphWizard<InitialSetupDraft> wizard)
    {
        return wizard.WhenAnyValue(x => x.CurrentStep)
            .Select(step => step?.Title ?? Observable.Return(string.Empty))
            .Switch()
            .DistinctUntilChanged();
    }
}
