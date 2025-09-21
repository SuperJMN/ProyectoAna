using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Maintenance;

public partial class StudentsViewModel : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();
    [Reactive] private Course? selectedCourse;
    [Reactive] private Class? selectedClass;
    [Reactive] private Student? selectedStudent;

    public ReactiveCommand<Unit, Unit> AddStudent { get; }
    public ReactiveCommand<Unit, Unit> DeleteStudent { get; }

    readonly PersistenceService persistence;

    public StudentsViewModel(PersistenceService persistence)
    {
        this.persistence = persistence;
        AddStudent = ReactiveCommand.Create(DoAddStudent, this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null));
        DeleteStudent = ReactiveCommand.Create(DoDeleteStudent, this.WhenAnyValue(x => x.SelectedStudent).Select(s => s != null));
        Load();
    }

    async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses) Courses.Add(c);
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
        SelectedStudent = SelectedClass?.Students.FirstOrDefault();
    }

    void DoAddStudent()
    {
        if (SelectedClass == null) return;
        var idx = SelectedClass.Students.Count + 1;
        var student = new Student { Id = $"student-{idx}", Name = $"Student {idx}" };
        SelectedClass.Students.Add(student);
        SelectedStudent = student;
        Save();
    }

    void DoDeleteStudent()
    {
        if (SelectedClass == null || SelectedStudent == null) return;
        SelectedClass.Students.Remove(SelectedStudent);
        // Remove assessments for this student
        SelectedClass.Assessments = SelectedClass.Assessments.Where(a => a.StudentId != SelectedStudent.Id).ToList();
        SelectedStudent = null;
        Save();
    }

    async void Save()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}
