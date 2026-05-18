using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupCourseClassesViewModel
{
    public InitialSetupCourseClassesViewModel(InitialSetupCourseDraft course)
    {
        CourseName = course.Name;
        CourseNumber = course.Number;
        Classes = new ObservableCollection<InitialSetupClassOptionViewModel>(
            ClassNames.Select(name => new InitialSetupClassOptionViewModel(name)));

        IsValid = Classes
            .Select(cls => cls.WhenAnyValue(x => x.IsSelected))
            .CombineLatest(values => values.Any(selected => selected))
            .DistinctUntilChanged();
    }

    public string CourseName { get; }

    public int? CourseNumber { get; }

    public ObservableCollection<InitialSetupClassOptionViewModel> Classes { get; }

    public IObservable<bool> IsValid { get; }

    public InitialSetupCourseClassesDraft CreateDraft()
    {
        return new InitialSetupCourseClassesDraft(
            CourseName,
            CourseNumber,
            Classes.Where(cls => cls.IsSelected).Select(cls => cls.Name).ToList());
    }

    private static readonly string[] ClassNames = ["A", "B", "C", "D", "E", "F"];
}
