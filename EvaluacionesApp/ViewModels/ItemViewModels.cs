using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.ViewModels;

public partial class CourseVm : ViewModelBase
{
    public Course Model { get; }
    public ObservableCollection<ClassVm> Classes { get; } = new();
    public ObservableCollection<CriterionVm> Criteria { get; } = new();

    [Reactive]
    private string id = string.Empty;

    [Reactive]
    private string name = string.Empty;

    public CourseVm(Course model)
    {
        Model = model;
        id = model.Id;
        name = model.Name;
        foreach (var c in model.Classes)
        {
            Classes.Add(new ClassVm(c));
        }
        foreach (var cr in model.Criteria)
        {
            Criteria.Add(new CriterionVm(cr));
        }

        this.WhenAnyValue(x => x.Name).Subscribe(v => Model.Name = v);
        this.WhenAnyValue(x => x.Id).Subscribe(v => Model.Id = v);
    }


    public Course ToModel()
    {
        Model.Classes = Classes.Select(x => x.ToModel()).ToList();
        Model.Criteria = Criteria.Select(x => x.ToModel()).ToList();
        Model.Name = Name;
        Model.Id = Id;
        return Model;
    }
}

public partial class ClassVm : ViewModelBase
{
    public Class Model { get; }
    public ObservableCollection<StudentVm> Students { get; } = new();

    [Reactive]
    private string id = string.Empty;

    [Reactive]
    private string name = string.Empty;

    public ClassVm(Class model)
    {
        Model = model;
        id = model.Id;
        name = model.Name;
        foreach (var s in model.Students)
        {
            Students.Add(new StudentVm(s));
        }

        this.WhenAnyValue(x => x.Name).Subscribe(v => Model.Name = v);
        this.WhenAnyValue(x => x.Id).Subscribe(v => Model.Id = v);
    }


    public Class ToModel()
    {
        Model.Students = Students.Select(x => x.ToModel()).ToList();
        return Model;
    }
}

public partial class StudentVm : ViewModelBase
{
    public Student Model { get; }

    [Reactive]
    private string id = string.Empty;

    [Reactive]
    private string name = string.Empty;

    private int positivos;

    private int negativos;

    private string observaciones = string.Empty;

    public StudentVm(Student model)
    {
        Model = model;
        id = model.Id;
        name = model.Name;
        positivos = Math.Max(0, model.Positivos);
        negativos = Math.Max(0, model.Negativos);
        observaciones = model.Observaciones ?? string.Empty;
        this.WhenAnyValue(x => x.Name).Subscribe(v => Model.Name = v);
        this.WhenAnyValue(x => x.Id).Subscribe(v => Model.Id = v);
        this.WhenAnyValue(x => x.Positivos).Subscribe(v => Model.Positivos = v);
        this.WhenAnyValue(x => x.Negativos).Subscribe(v => Model.Negativos = v);
        this.WhenAnyValue(x => x.Observaciones).Subscribe(v => Model.Observaciones = v);
    }


    public Student ToModel()
    {
        Model.Name = Name;
        Model.Id = Id;
        Model.Positivos = Positivos;
        Model.Negativos = Negativos;
        Model.Observaciones = Observaciones;
        return Model;
    }

    public int Positivos
    {
        get => positivos;
        set => this.RaiseAndSetIfChanged(ref positivos, Math.Max(0, value));
    }

    public int Negativos
    {
        get => negativos;
        set => this.RaiseAndSetIfChanged(ref negativos, Math.Max(0, value));
    }

    public string Observaciones
    {
        get => observaciones;
        set => this.RaiseAndSetIfChanged(ref observaciones, value ?? string.Empty);
    }
}

public partial class CriterionVm : ViewModelBase
{
    public Criterion Model { get; }
    public ObservableCollection<CriterionVm> Children { get; } = new();

    [Reactive]
    private string id = string.Empty;

    [Reactive]
    private string name = string.Empty;

    [Reactive]
    private double weight;

    public bool IsLeaf => Children.Count == 0;

    public CriterionVm(Criterion model)
    {
        Model = model;
        id = model.Id;
        name = model.Name;
        weight = model.Weight;
        foreach (var c in model.Children)
        {
            Children.Add(new CriterionVm(c));
        }

        this.WhenAnyValue(x => x.Name).Subscribe(v => Model.Name = v);
        this.WhenAnyValue(x => x.Id).Subscribe(v => Model.Id = v);
        this.WhenAnyValue(x => x.Weight).Subscribe(v => Model.Weight = v);
    }


    public Criterion ToModel()
    {
        Model.Name = Name;
        Model.Id = Id;
        Model.Weight = Weight;
        Model.Children = Children.Select(x => x.ToModel()).ToList();
        return Model;
    }
}

public class ScoreRowVm : ViewModelBase
{
    public StudentVm Student { get; }
    public Dictionary<string, double?> Scores { get; } = new();

    public ScoreRowVm(StudentVm student)
    {
        Student = student;
    }
}