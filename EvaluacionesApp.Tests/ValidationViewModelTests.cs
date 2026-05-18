using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.Classes;
using EvaluacionesApp.Desktop.Features.Courses;
using EvaluacionesApp.Desktop.Features.Criteria;
using EvaluacionesApp.Desktop.Features.Students;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Tests.Support;

namespace EvaluacionesApp.Tests;

public sealed class ValidationViewModelTests
{
    [Fact]
    public void CoursesViewModel_reports_error_when_selected_course_name_is_empty()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new CoursesViewModel(store, new NullNotificationService());

        viewModel.SelectedCourse!.Name = "";

        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public void ClassesViewModel_reports_error_when_selected_class_name_is_empty()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new ClassesViewModel(store, new NullNotificationService());

        viewModel.SelectedClass!.Name = "";

        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public void StudentsViewModel_reports_error_when_selected_student_name_is_empty()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        var student = viewModel.SelectedStudent!;
        student.FirstName = "";
        student.LastName = "";

        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public async Task StudentsViewModel_selects_new_student_after_add()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.AddStudent.Execute();

        var student = Assert.Single(viewModel.StudentsSelection.SelectedItems);
        Assert.Same(student, viewModel.SelectedStudent);
        Assert.Equal("Alumno 2", student.FirstName);
    }

    [Fact]
    public async Task CoursesViewModel_creates_courses_with_teacher_facing_default_name()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        using var viewModel = new CoursesViewModel(store, new NullNotificationService());

        await viewModel.AddCourse.Execute();

        Assert.Equal("Curso 1", viewModel.SelectedCourse!.Name);
    }

    [Fact]
    public async Task ClassesViewModel_creates_classes_with_teacher_facing_default_name()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root
        {
            Courses =
            {
                new Course { Id = "course-1", Name = "1 ESO", Terms = { 1 } }
            }
        });
        using var viewModel = new ClassesViewModel(store, new NullNotificationService());

        await viewModel.AddClass.Execute();

        Assert.Equal("Clase 1", viewModel.SelectedClass!.Name);
    }

    [Fact]
    public async Task StudentsViewModel_keeps_new_student_selected_while_editing_name()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.AddStudent.Execute();
        var student = viewModel.SelectedStudent!;

        student.FirstName = "MCP Validacion";
        student.LastName = "Alumno Prueba";

        Assert.Same(student, viewModel.SelectedStudent);
        Assert.Same(student, Assert.Single(viewModel.StudentsSelection.SelectedItems));
        Assert.Equal("Alumno Prueba, MCP Validacion", student.FullName);
    }

    [Fact]
    public void StudentsViewModel_setting_selected_student_updates_single_selection()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());
        var student = viewModel.SelectedClass!.Students.Last();

        viewModel.SelectedStudent = student;

        Assert.Same(student, viewModel.SelectedStudent);
        Assert.Same(student, Assert.Single(viewModel.StudentsSelection.SelectedItems));
    }

    [Fact]
    public void StudentsViewModel_uses_the_compact_selection_source_for_selection_model()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        Assert.Same(viewModel.CompactSelectionStudents, viewModel.StudentsSelection.SelectionModel.Source);
        Assert.Equal(viewModel.SelectedClass!.Students.Select(student => student.Id), viewModel.CompactSelectionStudents.Select(student => student.Id));
    }

    [Fact]
    public void StudentsViewModel_selected_student_ignores_items_from_other_classes()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());
        var selectedStudent = viewModel.SelectedStudent!;
        var selectedClass = viewModel.SelectedClass!;
        var otherClass = viewModel.SelectedCourse!.Classes.Single(cls => !ReferenceEquals(cls, selectedClass));
        var otherStudent = Assert.Single(otherClass.Students);

        viewModel.SelectedStudent = otherStudent;

        Assert.Same(selectedStudent, viewModel.SelectedStudent);
        Assert.Same(selectedStudent, Assert.Single(viewModel.StudentsSelection.SelectedItems));
    }

    [Fact]
    public async Task StudentsViewModel_keeps_new_student_selected_after_add()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.AddStudent.Execute();

        var student = Assert.Single(viewModel.StudentsSelection.SelectedItems);
        Assert.Same(student, viewModel.SelectedStudent);
        Assert.Contains(student, viewModel.CompactSelectionStudents);
        Assert.Same(viewModel.CompactSelectionStudents, viewModel.StudentsSelection.SelectionModel.Source);
    }

    [Fact]
    public async Task StudentsViewModel_resets_selection_mode_when_selected_class_changes()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.EnterCompactSelectionMode.Execute();
        viewModel.SelectedClass = viewModel.SelectedCourse!.Classes[1];

        Assert.False(viewModel.IsCompactSelectionMode);
        Assert.NotNull(viewModel.SelectedStudent);
        Assert.Equal("student-2", viewModel.SelectedStudent!.Id);
        Assert.Same(viewModel.CompactSelectionStudents, viewModel.StudentsSelection.SelectionModel.Source);
        Assert.Equal(["student-2"], viewModel.CompactSelectionStudents.Select(student => student.Id));
    }

    [Fact]
    public async Task CriteriaViewModel_reports_error_when_selected_criterion_is_invalid()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        viewModel.SelectedNode = Assert.Single(viewModel.Criteria);
        viewModel.SelectedNode!.Criterion.Name = "";
        viewModel.SelectedNode.Criterion.Weight = -1m;
        await viewModel.Save.Execute();

        Assert.False(viewModel.SelectedCriterionNameValid);
        Assert.False(viewModel.SelectedCriterionWeightValid);
        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public async Task CriteriaViewModel_shows_root_criterion_after_add()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithoutCriteria());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        await viewModel.AddRootCriterion.Execute();

        var criterion = Assert.Single(viewModel.SelectedCourse!.Criteria);
        var node = Assert.Single(viewModel.Criteria);
        Assert.Same(criterion, node.Criterion);
        Assert.Equal("Criterio 1", criterion.Name);
    }

    [Fact]
    public async Task CriteriaViewModel_add_child_confirms_and_removes_parent_assessments_when_leaf_has_scores()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithScoredLeafCriterion());
        var dialog = new ConfirmingDialog();
        using var viewModel = new CriteriaViewModel(
            store,
            dialog,
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        viewModel.SelectedNode = Assert.Single(viewModel.Criteria);
        var initialSaveCount = store.SaveCount;

        await viewModel.AddChildCriterion.Execute();

        var parent = Assert.Single(viewModel.SelectedCourse!.Criteria);
        Assert.Single(parent.Children);
        Assert.DoesNotContain(
            viewModel.SelectedCourse.Classes.SelectMany(cls => cls.Assessments),
            assessment => assessment.CriterionId == parent.Id);
        Assert.Equal(1, dialog.ShowCount);
        Assert.True(store.SaveCount > initialSaveCount);
    }

    [Fact]
    public async Task CriteriaViewModel_add_child_cancels_when_parent_assessment_removal_is_rejected()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithScoredLeafCriterion());
        var dialog = new ConfirmingDialog(false);
        using var viewModel = new CriteriaViewModel(
            store,
            dialog,
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        viewModel.SelectedNode = Assert.Single(viewModel.Criteria);
        var initialSaveCount = store.SaveCount;

        await viewModel.AddChildCriterion.Execute();

        var parent = Assert.Single(viewModel.SelectedCourse!.Criteria);
        Assert.Empty(parent.Children);
        Assert.Contains(
            viewModel.SelectedCourse.Classes.SelectMany(cls => cls.Assessments),
            assessment => assessment.CriterionId == parent.Id && assessment.Score.HasValue);
        Assert.Equal(1, dialog.ShowCount);
        Assert.Equal(initialSaveCount, store.SaveCount);
    }

    [Fact]
    public async Task CriteriaViewModel_add_child_without_parent_scores_does_not_ask_confirmation()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        var dialog = new ConfirmingDialog();
        using var viewModel = new CriteriaViewModel(
            store,
            dialog,
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        viewModel.SelectedNode = Assert.Single(viewModel.Criteria);

        await viewModel.AddChildCriterion.Execute();

        var parent = Assert.Single(viewModel.SelectedCourse!.Criteria);
        var child = Assert.Single(parent.Children);
        Assert.Equal("Subcriterio 1", child.Name);
        Assert.Equal(0, dialog.ShowCount);
    }

    static Root CreateRoot()
    {
        return new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = { 1 },
                    Criteria =
                    {
                        new Criterion { Id = "criterion-1", Name = "Criterion 1", Weight = 1m, Term = 1 }
                    },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Students =
                            {
                                new Student { Id = "student-1", FirstName = "Ana", LastName = "Garcia" }
                            }
                        }
                    }
                }
            }
        };
    }

    static Root CreateRootWithScoredLeafCriterion()
    {
        return new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = { 1 },
                    Criteria =
                    {
                        new Criterion { Id = "criterion-1", Name = "Criterion 1", Weight = 1m, Term = 1 }
                    },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Students =
                            {
                                new Student { Id = "student-1", FirstName = "Ana", LastName = "Garcia" },
                                new Student { Id = "student-2", FirstName = "Luis", LastName = "Perez" }
                            },
                            Assessments =
                            {
                                new Assessment { StudentId = "student-1", CriterionId = "criterion-1", Score = 7m },
                                new Assessment { StudentId = "student-2", CriterionId = "criterion-1", Score = null }
                            }
                        },
                        new Class
                        {
                            Id = "class-b",
                            Name = "B",
                            Students =
                            {
                                new Student { Id = "student-3", FirstName = "Eva", LastName = "Lopez" }
                            },
                            Assessments =
                            {
                                new Assessment { StudentId = "student-3", CriterionId = "criterion-1", Score = 8m }
                            }
                        }
                    }
                }
            }
        };
    }

    static Root CreateRootWithoutCriteria()
    {
        return new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = { 1 },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Students =
                            {
                                new Student { Id = "student-1", FirstName = "Ana", LastName = "Garcia" }
                            }
                        }
                    }
                }
            }
        };
    }

    static Root CreateRootWithTwoClasses()
    {
        return new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = { 1 },
                    Criteria =
                    {
                        new Criterion { Id = "criterion-1", Name = "Criterion 1", Weight = 1m, Term = 1 }
                    },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A",
                            Students =
                            {
                                new Student { Id = "student-1", FirstName = "Ana", LastName = "Garcia" }
                            }
                        },
                        new Class
                        {
                            Id = "class-b",
                            Name = "B",
                            Students =
                            {
                                new Student { Id = "student-2", FirstName = "Luis", LastName = "Perez" }
                            }
                        }
                    }
                }
            }
        };
    }
}
