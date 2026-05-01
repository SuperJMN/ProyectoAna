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
        Assert.Equal("Student 2", student.FirstName);
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
    public async Task StudentsViewModel_shows_details_for_selected_student_when_requested()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        Assert.False(viewModel.AreStudentDetailsShown);

        await viewModel.ShowSelectedStudentDetails.Execute();

        Assert.True(viewModel.AreStudentDetailsShown);
        Assert.NotNull(viewModel.SelectedStudent);
    }

    [Fact]
    public async Task StudentsViewModel_hides_details_without_clearing_selection()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());
        var selectedStudent = viewModel.SelectedStudent;

        await viewModel.ShowSelectedStudentDetails.Execute();
        await viewModel.ShowStudentsList.Execute();

        Assert.False(viewModel.AreStudentDetailsShown);
        Assert.Same(selectedStudent, viewModel.SelectedStudent);
        Assert.Same(selectedStudent, Assert.Single(viewModel.StudentsSelection.SelectedItems));
    }

    [Fact]
    public async Task StudentsViewModel_shows_details_for_new_student_after_add()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.AddStudent.Execute();

        Assert.True(viewModel.AreStudentDetailsShown);
        Assert.NotNull(viewModel.SelectedStudent);
    }

    [Fact]
    public async Task StudentsViewModel_resets_compact_state_when_selected_class_changes()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        await viewModel.ShowSelectedStudentDetails.Execute();
        viewModel.SelectedClass = viewModel.SelectedCourse!.Classes[1];

        Assert.False(viewModel.AreStudentDetailsShown);
        Assert.NotNull(viewModel.SelectedStudent);
        Assert.Equal("student-2", viewModel.SelectedStudent!.Id);
    }

    [Fact]
    public async Task CriteriaViewModel_reports_error_when_selected_criterion_is_invalid()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new CriteriaViewModel(store, new ConfirmingDialog(), new NullNotificationService());

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
