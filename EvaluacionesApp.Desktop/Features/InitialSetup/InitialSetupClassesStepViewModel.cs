using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupClassesStepViewModel
{
    public InitialSetupClassesStepViewModel()
        : this([new InitialSetupCourseDraft("1º ESO", 1), new InitialSetupCourseDraft("2º ESO", 2)])
    {
    }

    public InitialSetupClassesStepViewModel(IReadOnlyList<InitialSetupCourseDraft> courses)
    {
        Courses = new ObservableCollection<InitialSetupCourseClassesViewModel>(
            courses.Select(course => new InitialSetupCourseClassesViewModel(course)));

        IsValid = Courses
            .Select(course => course.IsValid)
            .CombineLatest(values => values.All(valid => valid))
            .DistinctUntilChanged();
    }

    public ObservableCollection<InitialSetupCourseClassesViewModel> Courses { get; }

    public IObservable<bool> IsValid { get; }

    public InitialSetupDraft CreateDraft()
    {
        return new InitialSetupDraft(Courses.Select(course => course.CreateDraft()).ToList());
    }
}
