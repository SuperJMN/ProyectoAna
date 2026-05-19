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
    public async Task CoursesViewModel_exposes_empty_state_until_first_course_is_added()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        using var viewModel = new CoursesViewModel(store, new NullNotificationService());

        Assert.False(viewModel.HasCourses);
        Assert.True(viewModel.ShowCoursesEmptyState);
        Assert.False(viewModel.ShowCourseList);
        Assert.False(viewModel.ShowCourseDetails);
        Assert.False(viewModel.ShowToolbarAddCourseAction);

        await viewModel.AddCourse.Execute();

        Assert.True(viewModel.HasCourses);
        Assert.False(viewModel.ShowCoursesEmptyState);
        Assert.True(viewModel.ShowCourseList);
        Assert.True(viewModel.ShowCourseDetails);
        Assert.True(viewModel.ShowToolbarAddCourseAction);
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
    public void ClassesViewModel_exposes_no_courses_state()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        using var viewModel = new ClassesViewModel(store, new NullNotificationService());

        Assert.False(viewModel.HasCourses);
        Assert.True(viewModel.ShowNoCoursesState);
        Assert.False(viewModel.ShowClassesEmptyState);
        Assert.False(viewModel.ShowClassList);
        Assert.False(viewModel.ShowClassDetails);
        Assert.False(viewModel.ShowToolbarAddClassAction);
    }

    [Fact]
    public async Task ClassesViewModel_exposes_empty_classes_state_until_first_class_is_added()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root
        {
            Courses =
            {
                new Course { Id = "course-1", Name = "1 ESO", Terms = { 1 } }
            }
        });
        using var viewModel = new ClassesViewModel(store, new NullNotificationService());

        Assert.True(viewModel.HasCourses);
        Assert.True(viewModel.HasSelectedCourse);
        Assert.False(viewModel.HasClassesForSelectedCourse);
        Assert.True(viewModel.ShowClassesEmptyState);
        Assert.False(viewModel.ShowClassList);
        Assert.False(viewModel.ShowClassDetails);
        Assert.False(viewModel.ShowToolbarAddClassAction);

        await viewModel.AddClass.Execute();

        Assert.True(viewModel.HasClassesForSelectedCourse);
        Assert.False(viewModel.ShowClassesEmptyState);
        Assert.True(viewModel.ShowClassList);
        Assert.True(viewModel.ShowClassDetails);
        Assert.True(viewModel.ShowToolbarAddClassAction);
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
    public void StudentsViewModel_exposes_action_hierarchy_for_selected_students()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        Assert.True(viewModel.HasSelectedStudents);
        Assert.True(viewModel.HasMoveTargets);
        Assert.True(viewModel.ShowToolbarAddStudentAction);
        Assert.True(viewModel.ShowEnterStudentSelectionAction);
        Assert.True(viewModel.ShowMoveStudentsAction);
        Assert.True(viewModel.ShowDeleteStudentAction);

        viewModel.SelectedStudent = null;

        Assert.False(viewModel.HasSelectedStudents);
        Assert.True(viewModel.ShowEnterStudentSelectionAction);
        Assert.False(viewModel.ShowMoveStudentsAction);
        Assert.False(viewModel.ShowDeleteStudentAction);
    }

    [Fact]
    public void StudentsViewModel_move_menu_excludes_the_current_class()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoClasses());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        var initialMenu = Assert.Single(viewModel.MoveStudentsMenu);
        Assert.Equal(["B"], initialMenu.Children.Select(item => item.Header));

        viewModel.SelectedClass = viewModel.SelectedCourse!.Classes.Single(cls => cls.Id == "class-b");

        var updatedMenu = Assert.Single(viewModel.MoveStudentsMenu);
        Assert.Equal(["A"], updatedMenu.Children.Select(item => item.Header));
    }

    [Fact]
    public void StudentsViewModel_hides_move_action_when_there_is_no_target_class()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        Assert.False(viewModel.HasMoveTargets);
        Assert.True(viewModel.HasSelectedStudents);
        Assert.False(viewModel.ShowMoveStudentsAction);
        Assert.True(viewModel.ShowDeleteStudentAction);
        Assert.Empty(viewModel.MoveStudentsMenu);
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
    public async Task StudentsViewModel_exposes_empty_state_until_first_student_is_added()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithoutStudents());
        using var viewModel = new StudentsViewModel(store, new NullNotificationService());

        Assert.True(viewModel.HasStudentsEmptyState);
        Assert.False(viewModel.HasStudentsInSelectedClass);
        Assert.True(viewModel.CanAddStudentsToSelectedClass);
        Assert.False(viewModel.ShowToolbarAddStudentAction);
        Assert.False(viewModel.ShowStudentsMasterDetails);
        Assert.False(viewModel.ShowCompactStudentsSelection);
        Assert.False(viewModel.ShowEnterStudentSelectionAction);
        Assert.False(viewModel.ShowMoveStudentsAction);
        Assert.False(viewModel.ShowDeleteStudentAction);
        Assert.Equal("La clase no tiene alumnos", viewModel.StudentsEmptyStateTitle);

        await viewModel.AddStudent.Execute();

        Assert.False(viewModel.HasStudentsEmptyState);
        Assert.True(viewModel.HasStudentsInSelectedClass);
        Assert.True(viewModel.ShowToolbarAddStudentAction);
        Assert.True(viewModel.ShowStudentsMasterDetails);
        Assert.True(viewModel.ShowEnterStudentSelectionAction);
        Assert.False(viewModel.ShowMoveStudentsAction);
        Assert.True(viewModel.ShowDeleteStudentAction);
        Assert.NotNull(viewModel.SelectedStudent);
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
    public async Task CriteriaViewModel_selects_root_criterion_after_add()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoCriteria());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);
        var initialRoot = viewModel.Criteria.Single(node => node.Criterion.Id == "criterion-1");
        viewModel.SelectedNode = initialRoot;

        await viewModel.AddRootCriterion.Execute();

        var created = viewModel.SelectedCourse!.Criteria.Single(criterion => criterion.Name == "Criterio 3");
        Assert.NotNull(viewModel.SelectedNode);
        Assert.Same(created, viewModel.SelectedNode!.Criterion);
    }

    [Fact]
    public void CriteriaViewModel_exposes_no_selection_state_for_empty_criteria()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithoutCriteria());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        Assert.True(viewModel.HasNoCriteriaForSelectedTerm);
        Assert.True(viewModel.ShowAddRootCriterionAction);
        Assert.False(viewModel.ShowCopyCriteriaAction);
        Assert.False(viewModel.HasSelectedCriterion);
        Assert.False(viewModel.ShowAddChildCriterionAction);
        Assert.False(viewModel.SelectedCriterionCanBeDeleted);
        Assert.False(viewModel.ShowDeleteCriterionAction);
        Assert.False(viewModel.SelectedCriterionHasChildren);
    }

    [Fact]
    public void CriteriaViewModel_hides_copy_action_when_only_target_is_current_term()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRoot());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        Assert.False(viewModel.HasCopyCriteriaTargets);
        Assert.False(viewModel.ShowCopyCriteriaAction);
        Assert.Empty(viewModel.CopyFeature.CopyCriteriaMenu);
    }

    [Fact]
    public void CriteriaViewModel_copy_menu_excludes_only_the_current_course_term()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithCopyTargets());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        var menusByHeader = viewModel.CopyFeature.CopyCriteriaMenu.ToDictionary(menu => menu.Header);

        Assert.True(viewModel.HasCopyCriteriaTargets);
        Assert.True(viewModel.ShowCopyCriteriaAction);
        Assert.Equal(["1 ESO", "2 ESO"], viewModel.CopyFeature.CopyCriteriaMenu.Select(menu => menu.Header));
        Assert.Equal(["Trimestre 2"], menusByHeader["1 ESO"].Children.Select(item => item.Header));
        Assert.Equal(["Trimestre 1"], menusByHeader["2 ESO"].Children.Select(item => item.Header));
    }

    [Fact]
    public async Task CriteriaViewModel_copy_to_current_course_term_does_not_mutate_or_save()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoTerms());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);
        var course = viewModel.SelectedCourse!;
        var initialCriterionIds = course.Criteria.Select(criterion => criterion.Id).ToArray();
        var initialSaveCount = store.SaveCount;

        await viewModel.CopyFeature.CopyCriteria.Execute(new CriterionCopyTarget(course, viewModel.SelectedTerm));

        Assert.Equal(initialSaveCount, store.SaveCount);
        Assert.Equal(initialCriterionIds, course.Criteria.Select(criterion => criterion.Id));
    }

    [Fact]
    public async Task CriteriaViewModel_selects_child_after_adding_it_to_selected_parent()
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

        await viewModel.AddChildCriterion.Execute();

        var parent = Assert.Single(viewModel.SelectedCourse!.Criteria);
        var child = Assert.Single(parent.Children);
        Assert.Same(child, viewModel.SelectedNode!.Criterion);
        Assert.True(viewModel.HasSelectedCriterion);
        Assert.True(viewModel.ShowAddChildCriterionAction);
        Assert.False(viewModel.SelectedCriterionHasChildren);
        Assert.True(viewModel.SelectedCriterionCanBeDeleted);
        Assert.True(viewModel.ShowDeleteCriterionAction);
        Assert.True(await viewModel.DeleteCriterion.CanExecute.FirstAsync());
    }

    [Fact]
    public async Task CriteriaViewModel_adds_child_to_context_node_and_selects_new_child()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoCriteria());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);
        var firstRoot = viewModel.Criteria.Single(node => node.Criterion.Id == "criterion-1");
        var secondRoot = viewModel.Criteria.Single(node => node.Criterion.Id == "criterion-2");
        viewModel.SelectedNode = firstRoot;

        await viewModel.AddChildCriterionToNode.Execute(secondRoot);

        Assert.Empty(firstRoot.Criterion.Children);
        var created = Assert.Single(secondRoot.Criterion.Children);
        Assert.Equal("Subcriterio 1", created.Name);
        Assert.NotNull(viewModel.SelectedNode);
        Assert.Same(created, viewModel.SelectedNode!.Criterion);
    }

    [Fact]
    public void CriteriaViewModel_exposes_action_hierarchy_for_selected_leaf_criterion()
    {
        using var store = RecordingSchoolStore.FromDomain(CreateRootWithTwoTerms());
        using var viewModel = new CriteriaViewModel(
            store,
            new ConfirmingDialog(),
            new NullNotificationService(),
            ImmediateScheduler.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

        viewModel.SelectedNode = Assert.Single(viewModel.Criteria);

        Assert.False(viewModel.HasNoCriteriaForSelectedTerm);
        Assert.True(viewModel.ShowAddRootCriterionAction);
        Assert.True(viewModel.ShowCopyCriteriaAction);
        Assert.True(viewModel.ShowAddChildCriterionAction);
        Assert.True(viewModel.SelectedCriterionCanBeDeleted);
        Assert.True(viewModel.ShowDeleteCriterionAction);
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

    static Root CreateRootWithTwoTerms()
    {
        return new Root
        {
            Courses =
            {
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = { 1, 2 },
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

    static Root CreateRootWithTwoCriteria()
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
                        new Criterion { Id = "criterion-1", Name = "Criterion 1", Weight = 1m, Term = 1 },
                        new Criterion { Id = "criterion-2", Name = "Criterion 2", Weight = 1m, Term = 1 }
                    },
                    Classes =
                    {
                        new Class
                        {
                            Id = "class-a",
                            Name = "A"
                        }
                    }
                }
            }
        };
    }

    static Root CreateRootWithCopyTargets()
    {
        var root = CreateRootWithTwoTerms();
        root.Courses.Add(new Course
        {
            Id = "course-2",
            Name = "2 ESO",
            Terms = { 1 },
            Classes =
            {
                new Class
                {
                    Id = "class-c",
                    Name = "C"
                }
            }
        });

        return root;
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

    static Root CreateRootWithoutStudents()
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
                            Name = "A"
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
