using System;
using System.Collections.Generic;

namespace EvaluacionesApp.Models;

public class Root
{
    public string Version { get; set; } = "1.0";
    public List<Course> Courses { get; set; } = new();
}

public class Course
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Class> Classes { get; set; } = new();
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
    public string Name { get; set; } = string.Empty;
    public int Positivos { get; set; }
    public int Negativos { get; set; }
    public string Observaciones { get; set; } = string.Empty;
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
