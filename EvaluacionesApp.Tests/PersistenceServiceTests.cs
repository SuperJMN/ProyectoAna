using System.Text.Json;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.Services;

namespace EvaluacionesApp.Tests;

public sealed class PersistenceServiceTests
{
    [Fact]
    public async Task Save_and_load_preserves_courses_classes_students_criteria_terms_and_scores()
    {
        var path = CreateDataPath();
        var service = new PersistenceService(path);
        var root = new Root
        {
            Version = "1.0",
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Number = 1,
                    Terms = { 2, 1 },
                    Criteria =
                    {
                        new Criterion
                        {
                            Id = "criterion-parent",
                            Name = "Parent",
                            Weight = 2m,
                            Term = 2,
                            Children =
                            {
                                new Criterion
                                {
                                    Id = "criterion-child",
                                    Name = "Child",
                                    Weight = 0.75m,
                                    Term = 2
                                }
                            }
                        }
                    },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Students =
                            {
                                new Student
                                {
                                    Id = "student-1",
                                    FirstName = "Ana",
                                    LastName = "Garcia",
                                    Positivos = 3,
                                    Negativos = 1,
                                    Observaciones = "Works well"
                                }
                            },
                            Assessments =
                            {
                                new Assessment
                                {
                                    StudentId = "student-1",
                                    CriterionId = "criterion-child",
                                    Score = 8.5m
                                }
                            }
                        }
                    }
                }
            }
        };

        await service.Save(root);

        var loaded = await service.Load();
        var course = Assert.Single(loaded.Courses);
        Assert.Equal("1.0", loaded.Version);
        Assert.Equal("course-1", course.Id);
        Assert.Equal("1 ESO", course.Name);
        Assert.Equal(1, course.Number);
        Assert.Equal(new[] { 1, 2 }, course.Terms);

        var cls = Assert.Single(course.Classes);
        Assert.Equal("class-a", cls.Id);
        Assert.Equal("A", cls.Name);

        var student = Assert.Single(cls.Students);
        Assert.Equal("Ana", student.FirstName);
        Assert.Equal("Garcia", student.LastName);
        Assert.Equal(3, student.Positivos);
        Assert.Equal(1, student.Negativos);
        Assert.Equal("Works well", student.Observaciones);

        var parent = Assert.Single(course.Criteria);
        Assert.Equal("criterion-parent", parent.Id);
        Assert.Equal("Parent", parent.Name);
        Assert.Equal(1m, parent.Weight);
        Assert.Equal(2, parent.Term);

        var child = Assert.Single(parent.Children);
        Assert.Equal("criterion-child", child.Id);
        Assert.Equal("Child", child.Name);
        Assert.Equal(0.75m, child.Weight);
        Assert.Equal(2, child.Term);

        var score = Assert.Single(cls.Assessments);
        Assert.Equal("student-1", score.StudentId);
        Assert.Equal("criterion-child", score.CriterionId);
        Assert.Equal(8.5m, score.Score);
    }

    [Fact]
    public async Task Save_deduplicates_scores_by_student_and_criterion_preserving_last_score()
    {
        var service = new PersistenceService(CreateDataPath());
        var root = new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Assessments =
                            {
                                new Assessment { StudentId = "student-1", CriterionId = "criterion-1", Score = 5m },
                                new Assessment { StudentId = "student-1", CriterionId = "criterion-1", Score = 7m }
                            }
                        }
                    }
                }
            }
        };

        await service.Save(root);
        var loaded = await service.Load();

        var cls = Assert.Single(Assert.Single(loaded.Courses).Classes);
        var score = Assert.Single(cls.Assessments);
        Assert.Equal("student-1", score.StudentId);
        Assert.Equal("criterion-1", score.CriterionId);
        Assert.Equal(7m, score.Score);
    }

    [Fact]
    public async Task Load_quarantines_corrupt_json_and_propagates_the_error()
    {
        var path = CreateDataPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ invalid json");

        var service = new PersistenceService(path);

        await Assert.ThrowsAsync<JsonException>(() => service.Load());
        var directory = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileName(path);
        var quarantine = Assert.Single(Directory.GetFiles(directory, $"{fileName}.corrupt-*.json"));
        Assert.Equal("{ invalid json", await File.ReadAllTextAsync(quarantine));
    }

    static string CreateDataPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "evaluaciones-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "persistencia.json");
    }
}
