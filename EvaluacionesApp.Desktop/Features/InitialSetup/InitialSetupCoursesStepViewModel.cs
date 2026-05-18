using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupCoursesStepViewModel
{
    public InitialSetupCoursesStepViewModel()
    {
        Courses = new ObservableCollection<InitialSetupCourseOptionViewModel>(
            Presets.Select(preset => new InitialSetupCourseOptionViewModel(preset.Name, preset.Number)));

        IsValid = Courses
            .Select(course => course.WhenAnyValue(x => x.IsSelected))
            .CombineLatest(values => values.Any(selected => selected))
            .DistinctUntilChanged();
    }

    public ObservableCollection<InitialSetupCourseOptionViewModel> Courses { get; }

    public IObservable<bool> IsValid { get; }

    public IReadOnlyList<InitialSetupCourseDraft> CreateDraft()
    {
        return Courses
            .Where(course => course.IsSelected)
            .Select(course => course.CreateDraft())
            .ToList();
    }

    private static readonly InitialSetupCourseDraft[] Presets =
    [
        new("1º ESO", 1),
        new("2º ESO", 2),
        new("3º ESO", 3),
        new("4º ESO", 4),
        new("1º Bachillerato", 1),
        new("2º Bachillerato", 2),
    ];
}
