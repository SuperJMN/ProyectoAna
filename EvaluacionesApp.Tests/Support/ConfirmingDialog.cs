using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace EvaluacionesApp.Tests.Support;

public sealed class ConfirmingDialog : IDialog
{
    public Task<bool> Show<TViewModel>(
        Maybe<TViewModel> viewModel,
        Maybe<IObservable<string>> title,
        Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
        Maybe<object> icon = default,
        DialogTone tone = DialogTone.Neutral,
        DialogSize size = DialogSize.Auto)
    {
        return Task.FromResult(true);
    }
}
