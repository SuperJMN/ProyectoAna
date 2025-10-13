using System;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.Services;
using Xunit;

namespace EvaluacionesApp.Tests;

public class SanityCheckerTests
{
    [Fact]
    public void Returns_empty_report_when_there_are_no_parent_scores()
    {
        var studentId = Guid.NewGuid().ToString();
        var root = new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Name = "Course",
                    Criteria =
                    [
                        new Criterion
                        {
                            Id = "parent",
                            Name = "Parent",
                            Weight = 1m,
                            Term = 1,
                            Children =
                            [
                                new Criterion { Id = "child-1", Name = "Child 1", Weight = 0.5m, Term = 1 },
                                new Criterion { Id = "child-2", Name = "Child 2", Weight = 0.5m, Term = 1 }
                            ]
                        }
                    ],
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-1",
                            Name = "A",
                            Students =
                            [
                                new Student { Id = studentId, FirstName = "Test" }
                            ],
                            Assessments =
                            [
                                new Assessment { StudentId = studentId, CriterionId = "child-1", Score = 5m },
                                new Assessment { StudentId = studentId, CriterionId = "child-2", Score = 7m }
                            ]
                        }
                    ]
                }
            ]
        };

        var report = SanityChecker.Analyze(root);

        Assert.False(report.HasIssues);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Reports_parent_scores_with_student_names()
    {
        var studentId = Guid.NewGuid().ToString();
        var root = new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Name = "Course",
                    Criteria =
                    [
                        new Criterion
                        {
                            Id = "parent",
                            Name = "Parent",
                            Weight = 1m,
                            Term = 1,
                            Children =
                            [
                                new Criterion { Id = "child-1", Name = "Child 1", Weight = 0.5m, Term = 1 }
                            ]
                        }
                    ],
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-1",
                            Name = "A",
                            Students =
                            [
                                new Student { Id = studentId, FirstName = "Ana", LastName = "Doe" }
                            ],
                            Assessments =
                            [
                                new Assessment { StudentId = studentId, CriterionId = "child-1", Score = 6m },
                                new Assessment { StudentId = studentId, CriterionId = "parent", Score = 2m }
                            ]
                        }
                    ]
                }
            ]
        };

        var report = SanityChecker.Analyze(root);

        Assert.True(report.HasIssues);
        var warning = Assert.Single(report.Warnings);
        Assert.Contains("parent", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Doe Ana", warning, StringComparison.Ordinal);
    }
}
