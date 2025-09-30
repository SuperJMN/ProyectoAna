using System;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicStudent : ReactiveObject
{
    private string id;
    private string name;

    public DynamicStudent(Student model)
    {
        id = model.Id;
        name = model.Name;
    }

    public string Id
    {
        get => id;
        set
        {
            if (value == id)
            {
                return;
            }
            var previous = id;
            this.RaiseAndSetIfChanged(ref id, value);
            IdChanged?.Invoke(this, previous);
        }
    }

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public event Action<DynamicStudent, string>? IdChanged;

    public Student ToDomain()
    {
        return new Student
        {
            Id = id,
            Name = name
        };
    }
}
