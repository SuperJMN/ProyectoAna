using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;

namespace EvaluacionesApp.Desktop.Features.Criteria;

public partial class CriteriaQuickSetupViewModel : ReactiveValidationObject, IValidatable, IHaveTitle
{
    [Reactive] private string criteriaText = string.Empty;

    public CriteriaQuickSetupViewModel()
    {
        this.ValidationRule(
            this.WhenAnyValue(x => x.CriteriaText)
                .Select(text => GetCriteriaNames(text).Count > 0),
            "Anade al menos un criterio");
    }

    public IObservable<bool> IsValid => ValidationContext.Valid;

    public IObservable<string> Title => Observable.Return("Crear criterios");

    public IReadOnlyList<string> CriteriaNames => GetCriteriaNames(CriteriaText);

    static IReadOnlyList<string> GetCriteriaNames(string? text)
    {
        return CriteriaNameParser.Parse(text);
    }
}
