using System;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicStudent : ReactiveObject
{
    private readonly string id;
    private string name;
    private int positivos;
    private int negativos;
    private string observaciones;

    public DynamicStudent(Student model)
    {
        id = model.Id;
        name = model.Name;
        positivos = Math.Max(0, model.Positivos);
        negativos = Math.Max(0, model.Negativos);
        observaciones = model.Observaciones ?? string.Empty;
    }

    public string Id => id;

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
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
