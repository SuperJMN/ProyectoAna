using System;
using System.Linq;
using EvaluacionesApp.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Dynamic;

public partial class DynamicStudent : ReactiveObject
{
    private readonly string id;

    private string firstName;
    private string lastName;
    [Reactive] private int positivos;
    [Reactive] private int negativos;
    [Reactive] private string observaciones;

    public DynamicStudent(Student model)
    {
        id = model.Id;
        firstName = model.FirstName;
        lastName = model.LastName;
        positivos = Math.Max(0, model.Positivos);
        negativos = Math.Max(0, model.Negativos);
        observaciones = model.Observaciones;
    }

    public string Id => id;

    public string FirstName
    {
        get => firstName;
        set
        {
            var sanitized = value?.Trim() ?? string.Empty;
            if (sanitized == firstName)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref firstName, sanitized);
            this.RaisePropertyChanged(nameof(FullName));
        }
    }

    public string LastName
    {
        get => lastName;
        set
        {
            var sanitized = value?.Trim() ?? string.Empty;
            if (sanitized == lastName)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref lastName, sanitized);
            this.RaisePropertyChanged(nameof(FullName));
        }
    }

    public string FullName => string.Join(" ", new[] { LastName, FirstName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    public Student ToDomain()
    {
        return new Student
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Positivos = positivos,
            Negativos = negativos,
            Observaciones = observaciones
        };
    }
}
