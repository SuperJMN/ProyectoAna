using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.Features.Grades;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Tests.Support;
using Xunit;

namespace EvaluacionesApp.Tests;

public class CriterionScoreAggregatorTests
{
    [Fact]
    public async Task Aggregates_children_scores_with_weights()
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
                                new Assessment { StudentId = studentId, CriterionId = "child-1", Score = 10m },
                                new Assessment { StudentId = studentId, CriterionId = "child-2", Score = 30m }
                            ]
                        }
                    ]
                }
            ]
        };

        using var store = TestStoreFactory.Create(root);
        var course = store.Root.Courses[0];
        var cls = course.Classes[0];
        var student = cls.Students[0];
        var leaves = course.EnumerateLeafCriteria(1).ToList();
        var row = new ScoreRow(cls, student, leaves, 1, ImmediateScheduler.Instance);
        var node = ScopedCriterionNode.Build(course.Criteria.First(), 1)!;

        var result = CriterionScoreAggregator.Compute(node, row);

        Assert.Equal(20m, result);
    }

    [Fact]
    public async Task Ignores_parent_local_score_when_children_are_present()
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
                                new Criterion { Id = "child-1", Name = "Child 1", Weight = 0.25m, Term = 1 },
                                new Criterion { Id = "child-2", Name = "Child 2", Weight = 0.75m, Term = 1 }
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
                                new Assessment { StudentId = studentId, CriterionId = "child-1", Score = 4m },
                                new Assessment { StudentId = studentId, CriterionId = "child-2", Score = 8m },
                                new Assessment { StudentId = studentId, CriterionId = "parent", Score = 2m }
                            ]
                        }
                    ]
                }
            ]
        };

        using var store = TestStoreFactory.Create(root);
        var course = store.Root.Courses[0];
        var cls = course.Classes[0];
        var student = cls.Students[0];
        var leaves = course.EnumerateLeafCriteria(1).ToList();
        var row = new ScoreRow(cls, student, leaves, 1, ImmediateScheduler.Instance);
        var node = ScopedCriterionNode.Build(course.Criteria.First(), 1)!;

        var result = CriterionScoreAggregator.Compute(node, row);

        Assert.Equal(4m * 0.25m + 8m * 0.75m, result);
    }
}
