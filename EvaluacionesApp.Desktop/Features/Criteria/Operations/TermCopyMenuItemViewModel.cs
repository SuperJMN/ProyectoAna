using System;
using System.Reactive;
using EvaluacionesApp.Desktop.ViewModels;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.Criteria.Operations;

public sealed class TermCopyMenuItemViewModel : MenuItemViewModel, IDisposable
{
    public TermCopyMenuItemViewModel(TermCriterionCopyTarget term, ReactiveCommand<CriterionCopyTarget?, Unit> command)
    {
        Key = $"{term.Target.Course.Id}:{term.Target.Term}";
        Header = term.TermName;
        Command = command;
        CommandParameter = term.Target;
    }

    public void Dispose()
    {
    }
}
