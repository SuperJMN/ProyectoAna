using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Maintenance;

public partial class ClassesViewModel : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();
    [Reactive] private Course? selectedCourse;
    [Reactive] private Class? selectedClass;

    public ReactiveCommand<Unit, Unit> AddClass { get; }

    readonly PersistenceService persistence;

    public ClassesViewModel(PersistenceService persistence)
    {
        this.persistence = persistence;
        AddClass = ReactiveCommand.Create(DoAddClass, this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null));
        Load();
    }

    async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses) Courses.Add(c);
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
    }

    void DoAddClass()
    {
        if (SelectedCourse == null) return;
        var idx = SelectedCourse.Classes.Count + 1;
        var cls = new Class { Id = $"{SelectedCourse.Id}-class-{idx}", Name = $"Class {idx}" };
        SelectedCourse.Classes.Add(cls);
        SelectedClass = cls;
        Save();
    }

    async void Save()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}
