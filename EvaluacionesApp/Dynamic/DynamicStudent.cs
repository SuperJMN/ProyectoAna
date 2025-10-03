using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicStudent : ReactiveObject
{
    private readonly string id;
    private string name;

    public DynamicStudent(Student model)
    {
        id = model.Id;
        name = model.Name;
    }

    public string Id => id;

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public Student ToDomain()
    {
        return new Student
        {
            Id = id,
            Name = name
        };
    }
}
