using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using EvaluacionesApp.Dynamic;
using EvaluacionesApp.Models;
using EvaluacionesApp.Tests.Support;
using EvaluacionesApp.Views.Grades;
using Xunit;

namespace EvaluacionesApp.Tests;

public class ScoreRowTests
{
    [Fact]
    public async Task Calculates_totals_reactively()
    {
        var scheduler = ImmediateScheduler.Instance;
        var root = new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Criteria =
                    [
                        new Criterion { Id = "crit-1", Weight = 2, ClassId = "class-1", Term = 1 },
                        new Criterion { Id = "crit-2", Weight = 1, ClassId = "class-1", Term = 1 }
                    ],
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-1",
                            Students =
                            [
                                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Ana" }
                            ]
                        }
                    ]
                }
            ]
        };

        using var store = TestStoreFactory.Create(root);
        var course = (await store.GetRoot()).Courses[0];
        var cls = course.Classes[0];
        var student = cls.Students[0];
        var leaves = new List<DynamicCriterion>(course.Criteria);

        var row = new ScoreRow(cls, student, leaves, 1, scheduler);
        row.SetWeights(new Dictionary<string, double>
        {
            ["crit-1"] = 0.5,
            ["crit-2"] = 0.5
        });

        Assert.Equal(0, row.Total);
        row["crit-1"].Value = 8;
        row["crit-2"].Value = 4;
        Assert.Equal(6, row.Total);

        row.SetWeights(new Dictionary<string, double>
        {
            ["crit-1"] = 0.8,
            ["crit-2"] = 0.2
        });

        Assert.Equal(7.2, row.Total, 2);
    }

    [Fact]
    public async Task Creates_missing_assessments_on_demand()
    {
        var scheduler = ImmediateScheduler.Instance;
        var root = new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Criteria =
                    [
                        new Criterion { Id = "crit-1", Weight = 1, ClassId = "class-1", Term = 1 }
                    ],
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-1",
                            Students =
                            [
                                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Ana" }
                            ]
                        }
                    ]
                }
            ]
        };

        using var store = TestStoreFactory.Create(root);
        var course = (await store.GetRoot()).Courses[0];
        var cls = course.Classes[0];
        var student = cls.Students[0];
        var row = new ScoreRow(cls, student, course.Criteria, 1, scheduler);

        var binding = row["crit-extra"];

        Assert.Null(binding.Value);
        Assert.Contains(cls.Assessments, assessment =>
            assessment.StudentId == student.Id && assessment.CriterionId == "crit-extra" && assessment.Term == 1);
    }
}
