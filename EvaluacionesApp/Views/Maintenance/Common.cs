using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Maintenance;

public partial class CoursesViewModelBase : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();
    [Reactive] private Course? selectedCourse;

    protected readonly PersistenceService persistence;

    public CoursesViewModelBase(PersistenceService persistence)
    {
        this.persistence = persistence;
        Load();
    }

    protected async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses) Courses.Add(c);
        SelectedCourse = Courses.FirstOrDefault();
    }

    protected async void Save()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}
