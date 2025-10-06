using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace EvaluacionesApp.Desktop.Models;

public class Root
{
    public string Version { get; set; } = "1.0";
    public List<Course> Courses { get; set; } = new();
}

public class Course
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Number { get; set; }
    public List<Class> Classes { get; set; } = new();
    public List<int> Terms { get; set; } = new();
    public List<Criterion> Criteria { get; set; } = new();
}

public class Class
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Student> Students { get; set; } = new();
    public List<Assessment> Assessments { get; set; } = new();
}

public class Student
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Positivos { get; set; }
    public int Negativos { get; set; }
    public string Observaciones { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyName
    {
        get => null;
        set => ApplyLegacyName(value);
    }

    [JsonIgnore]
    public string FullName => string.Join(" ", new[] { LastName, FirstName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    void ApplyLegacyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        FirstName = parts[0];
        LastName = parts.Length > 1 ? parts[1] : string.Empty;
    }
}

public class Criterion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; } = 0;
    public string? ClassId { get; set; } = string.Empty;
    public int? Term { get; set; }
    public List<Criterion> Children { get; set; } = new();
}

public class Assessment
{
    public string StudentId { get; set; } = string.Empty;
    public string CriterionId { get; set; } = string.Empty;
    public double? Score { get; set; }
    public int? Term { get; set; }
}
