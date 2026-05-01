using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Features.Criteria;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Tests.Support;
using EvaluacionesApp.Desktop.Features.Grades.Converters;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Desktop.Features.Grades;
using Microsoft.Reactive.Testing;

namespace EvaluacionesApp.Tests;

public class GradesViewModelTests
{
    [Fact]
    public async Task Initialization_populates_courses_classes_and_rows()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        Assert.Equal(2, viewModel.Courses.Count);
        Assert.Equal("course-1", viewModel.SelectedCourse?.Id);
        Assert.Equal("class-1a", viewModel.SelectedClass?.Id);
        Assert.Equal(3, viewModel.LeafCriteria.Count);
        Assert.Equal(2, viewModel.ScoreRows.Count);
    }

    [Fact]
    public async Task Changing_course_updates_selected_class()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        viewModel.SelectedCourse = viewModel.Courses.Last();
        Pump(scheduler);

        Assert.Equal(viewModel.SelectedCourse?.Classes.First(), viewModel.SelectedClass);
    }

    [Fact]
    public async Task Adding_criterion_from_criteria_view_updates_grades_reactively()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var gradesViewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);
        using var criteriaViewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            scheduler,
            TimeSpan.Zero,
            TimeSpan.Zero);

        await gradesViewModel.Initialization;
        Pump(scheduler);

        var initialLeafCount = gradesViewModel.LeafCriteria.Count;

        await criteriaViewModel.AddRootCriterion.Execute();
        Pump(scheduler);

        var newCriterion = criteriaViewModel.SelectedCourse!.Criteria.Last();
        Assert.Equal(initialLeafCount + 1, gradesViewModel.LeafCriteria.Count);
        Assert.Contains(gradesViewModel.LeafCriteria, criterion => criterion.Id == newCriterion.Id);
    }

    [Fact]
    public async Task Adding_student_rebuilds_rows()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var cls = viewModel.SelectedClass!;
        var newStudentId = Guid.NewGuid().ToString();
        cls.AddStudent(new Student { Id = newStudentId, FirstName = "Student", LastName = "3" });

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(250).Ticks);
        Pump(scheduler);

        Assert.Contains(viewModel.ScoreRows, row => row.Student.Id == newStudentId);
    }

    [Fact]
    public async Task OpenScoreDetails_shows_details_for_selected_row()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.Last();

        Assert.False(viewModel.AreScoreDetailsShown);

        await viewModel.OpenScoreDetails.Execute(row);

        Assert.True(viewModel.AreScoreDetailsShown);
        Assert.Same(row, viewModel.SelectedScoreRow);
    }

    [Fact]
    public async Task ShowScoreRows_hides_details_without_clearing_selection()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.First();

        await viewModel.OpenScoreDetails.Execute(row);
        await viewModel.ShowScoreRows.Execute();

        Assert.False(viewModel.AreScoreDetailsShown);
        Assert.Same(row, viewModel.SelectedScoreRow);
    }

    [Fact]
    public async Task Changing_class_hides_score_details()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        await viewModel.OpenScoreDetails.Execute(viewModel.ScoreRows.First());

        viewModel.SelectedClass = viewModel.SelectedCourse!.Classes.Last();
        Pump(scheduler);

        Assert.False(viewModel.AreScoreDetailsShown);
    }

    [Fact]
    public async Task Switching_term_excludes_legacy_criteria_from_other_terms()
    {
        var scheduler = new TestScheduler();
        var legacyCourse = new Course
        {
            Id = "course-legacy",
            Name = "Legacy Course",
            Classes =
            [
                new Class
                {
                    Id = "class-legacy",
                    Name = "Legacy Class",
                    Students =
                    [
                        new Student { Id = Guid.NewGuid().ToString(), FirstName = "Student" }
                    ],
                    Assessments = new List<Assessment>()
                }
            ],
            Criteria =
            [
                new Criterion { Id = "legacy", Name = "Legacy", Weight = 1, ClassId = string.Empty, Term = null },
                new Criterion { Id = "term-2", Name = "Term 2", Weight = 1, ClassId = string.Empty, Term = 2 }
            ]
        };

        var root = new Root { Courses = [legacyCourse] };

        using var store = RecordingSchoolStore.FromDomain(root);
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        Assert.Contains(viewModel.LeafCriteria, criterion => criterion.Id == "legacy");

        viewModel.SelectedTerm = 2;
        Pump(scheduler);

        Assert.DoesNotContain(viewModel.LeafCriteria, criterion => criterion.Id == "legacy");
        Assert.Contains(viewModel.LeafCriteria, criterion => criterion.Id == "term-2");
    }

    [Fact]
    public async Task Updating_score_triggers_save()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var criterion = viewModel.LeafCriteria.First();
        var row = viewModel.ScoreRows.First();

        var initialSaveCount = store.SaveCount;

        row[criterion.Id].Value = 7m;

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        Pump(scheduler);

        Assert.Equal(initialSaveCount + 1, store.SaveCount);
    }

    [Fact]
    public async Task Save_requests_are_throttled_when_interval_is_set()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        var interval = TimeSpan.FromMilliseconds(500);
        using var viewModel = new GradesViewModel(store, scheduler, interval);

        await viewModel.Initialization;
        Pump(scheduler);

        var criterion = viewModel.LeafCriteria.First();
        var row = viewModel.ScoreRows.First();

        row[criterion.Id].Value = 8m;

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
        Pump(scheduler);

        Assert.Equal(0, store.SaveCount);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(600).Ticks);
        Pump(scheduler);

        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Score_rows_create_missing_assessments()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.First();
        var binding = row["criterion-extra"];

        Assert.Null(binding.Value);
        var cls = viewModel.SelectedClass!;
        Assert.Contains(cls.Assessments, assessment =>
            assessment.StudentId == row.Student.Id && assessment.CriterionId == "criterion-extra");
    }

    [Fact]
    public async Task Total_uses_all_leaf_scores()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.First();

        row["criterion-1a"].Value = 10m;
        row["criterion-1b"].Value = 4m;
        row["criterion-2"].Value = 6m;

        Pump(scheduler);

        Assert.Equal(7m, row.Total, 6);
    }

    [Fact]
    public async Task Total_respects_parent_weights_when_children_have_scores()
    {
        var scheduler = new TestScheduler();
        var root = new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-weighted",
                    Name = "Weighted Course",
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-weighted",
                            Name = "A",
                            Students =
                            [
                                new Student { Id = "student-weighted", FirstName = "Jaime" }
                            ],
                            Assessments = new List<Assessment>()
                        }
                    ],
                    Criteria =
                    [
                        new Criterion
                        {
                            Id = "c1",
                            Name = "C1",
                            Weight = 0.25m,
                            ClassId = string.Empty,
                            Term = 1,
                            Children =
                            [
                                new Criterion
                                {
                                    Id = "c1-1",
                                    Name = "C1.1",
                                    Weight = 0.5m,
                                    ClassId = string.Empty,
                                    Term = 1,
                                    Children =
                                    [
                                        new Criterion { Id = "c1-1-1", Name = "C1.1.1", Weight = 0.5m, ClassId = string.Empty, Term = 1 },
                                        new Criterion { Id = "c1-1-2", Name = "C1.1.2", Weight = 0.5m, ClassId = string.Empty, Term = 1 }
                                    ]
                                },
                                new Criterion
                                {
                                    Id = "c1-2",
                                    Name = "C1.2",
                                    Weight = 0.5m,
                                    ClassId = string.Empty,
                                    Term = 1,
                                    Children =
                                    [
                                        new Criterion { Id = "c1-2-1", Name = "C1.2.1", Weight = 0.5m, ClassId = string.Empty, Term = 1 },
                                        new Criterion { Id = "c1-2-2", Name = "C1.2.2", Weight = 0.5m, ClassId = string.Empty, Term = 1 }
                                    ]
                                }
                            ]
                        },
                        new Criterion { Id = "c2", Name = "C2", Weight = 0.75m, ClassId = string.Empty, Term = 1 }
                    ]
                }
            ]
        };

        using var store = RecordingSchoolStore.FromDomain(root);
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.First();

        row["c1-1-1"].Value = 10m;
        row["c1-1-2"].Value = 10m;
        row["c1-2-1"].Value = 10m;
        row["c1-2-2"].Value = 10m;

        Pump(scheduler);

        Assert.Equal(2.5m, row.Total, 4);
    }

    [Fact]
    public async Task Parent_criterion_score_updates_when_children_change()
    {
        var scheduler = new TestScheduler();
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new GradesViewModel(store, scheduler, TimeSpan.Zero);

        await viewModel.Initialization;
        Pump(scheduler);

        var row = viewModel.ScoreRows.First();
        var parent = viewModel.SelectedCourse!.Criteria.First(c => c.Id == "criterion-1");
        var node = ScopedCriterionNode.Build(parent, viewModel.SelectedTerm)!;
        var converter = new CriterionAggregateConverter();

        row["criterion-1a"].Value = 8m;
        row["criterion-1b"].Value = 4m;

        Pump(scheduler);

        var initial = converter.Convert(new object?[] { node, row }, typeof(string), null, CultureInfo.InvariantCulture) as string;
        Assert.Equal("6.67", initial);

        row["criterion-1a"].Value = 10m;

        Pump(scheduler);

        var updated = converter.Convert(new object?[] { node, row }, typeof(string), null, CultureInfo.InvariantCulture) as string;
        Assert.Equal("8.00", updated);
    }

    private static void Pump(TestScheduler scheduler)
    {
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
    }

    private static Root CreateRoot()
    {
        return new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Name = "Course 1",
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-1a",
                            Name = "Class 1A",
                            Students =
                            [
                                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Student", LastName = "1" },
                                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Student", LastName = "2" }
                            ],
                            Assessments = new List<Assessment>()
                        }
                    ],
                    Criteria =
                    [
                        new Criterion
                        {
                            Id = "criterion-1",
                            Name = "Criterion 1",
                            Weight = 1,
                            ClassId = string.Empty,
                            Term = 1,
                            Children =
                            [
                                new Criterion { Id = "criterion-1a", Name = "Criterion 1A", Weight = 2, ClassId = string.Empty, Term = 1 },
                                new Criterion { Id = "criterion-1b", Name = "Criterion 1B", Weight = 1, ClassId = string.Empty, Term = 1 }
                            ]
                        },
                        new Criterion { Id = "criterion-2", Name = "Criterion 2", Weight = 1, ClassId = string.Empty, Term = 1 }
                    ]
                },
                new Course
                {
                    Id = "course-2",
                    Name = "Course 2",
                    Classes =
                    [
                        new Class
                        {
                            Id = "class-2a",
                            Name = "Class 2A",
                            Students =
                            [
                                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Student", LastName = "4" }
                            ],
                            Assessments = new List<Assessment>()
                        }
                    ],
                    Criteria =
                    [
                        new Criterion { Id = "criterion-3", Name = "Criterion 3", Weight = 1, ClassId = string.Empty, Term = 1 }
                    ]
                }
            ]
        };
    }

    private static Root CreateRootWithTwoClasses()
    {
        var root = CreateRoot();
        root.Courses[0].Classes.Add(new Class
        {
            Id = "class-1b",
            Name = "Class 1B",
            Students =
            [
                new Student { Id = Guid.NewGuid().ToString(), FirstName = "Student", LastName = "3" }
            ],
            Assessments = new List<Assessment>()
        });

        return root;
    }
}
