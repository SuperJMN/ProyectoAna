using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Maintenance;

public partial class CoursesViewModel : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();
    [Reactive] private Course? selectedCourse;

    public ReactiveCommand<Unit, Unit> AddCourse { get; }

    readonly PersistenceService persistence;

    public CoursesViewModel(PersistenceService persistence)
    {
        this.persistence = persistence;
        AddCourse = ReactiveCommand.Create(DoAddCourse);
        Load();
    }

    async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses) Courses.Add(c);
        SelectedCourse = Courses.FirstOrDefault();
    }

    void DoAddCourse()
    {
        var idx = Courses.Count + 1;
        Courses.Add(new Course { Id = $"course-{idx}", Name = $"Course {idx}" });
        SelectedCourse = Courses.Last();
        Save();
    }

    async void Save()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}
