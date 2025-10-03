using System;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public partial class DynamicStudent : ReactiveObject
{
    private readonly string id;

    [Reactive] private string name;
    [Reactive] private int positivos;
    [Reactive] private int negativos;
    [Reactive] private string observaciones;

    public DynamicStudent(Student model)
    {
        id = model.Id;
        name = model.Name;
        positivos = Math.Max(0, model.Positivos);
        negativos = Math.Max(0, model.Negativos);
        observaciones = model.Observaciones;
    }

    public string Id => id;
    
    public Student ToDomain()
    {
        return new Student
        {
            Id = id,
            Name = name,
            Positivos = positivos,
            Negativos = negativos,
            Observaciones = observaciones
        };
    }
}
