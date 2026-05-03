using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace EvaluacionesApp.Tests.Support;

public sealed class ConfirmingDialog : IDialog
{
    public ConfirmingDialog(bool confirmationResult = true)
    {
        ConfirmationResult = confirmationResult;
    }

    public bool ConfirmationResult { get; }

    public int ShowCount { get; private set; }

    public Task<bool> Show<TViewModel>(
        Maybe<TViewModel> viewModel,
        Maybe<IObservable<string>> title,
        Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
        Maybe<object> icon = default,
        DialogTone tone = DialogTone.Neutral,
        DialogSize size = DialogSize.Auto)
    {
        ShowCount++;
        var closeable = new TestCloseable();
        var options = optionsFactory(viewModel, closeable).ToList();
        var selected = ConfirmationResult
            ? options.FirstOrDefault(option => option.Role == OptionRole.Primary) ?? options.FirstOrDefault()
            : options.FirstOrDefault(option => option.Role == OptionRole.Secondary || option.IsCancel) ?? options.FirstOrDefault();

        if (selected?.Command.CanExecute(null) == true)
        {
            selected.Command.Execute(null);
        }

        return Task.FromResult(true);
    }

    sealed class TestCloseable : ICloseable
    {
        public void Close()
        {
        }

        public void Dismiss()
        {
        }
    }
}
