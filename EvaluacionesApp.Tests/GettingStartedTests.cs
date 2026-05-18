using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.Classes;
using EvaluacionesApp.Desktop.Features.Courses;
using EvaluacionesApp.Desktop.Features.Criteria;
using EvaluacionesApp.Desktop.Features.GettingStarted;
using EvaluacionesApp.Desktop.Features.Grades;
using EvaluacionesApp.Desktop.Features.InitialSetup;
using EvaluacionesApp.Desktop.Features.Students;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Tests.Support;
using Reactive.Bindings;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Wizards.Graph.Core;
using Zafiro.UI;
using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Tests;

public sealed class GettingStartedTests
{
    [Fact]
    public async Task Getting_started_runs_initial_setup_and_updates_state()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        var selection = new SchoolSelectionState();
        var shell = new RecordingShell();
        var coordinator = new InitialSetupCoordinator(
            store,
            new CompletingInitialSetupDialog(),
            new InitialSetupWizardFactory(),
            new InitialSetupApplicator(store, selection),
            shell,
            new NullNotificationService());
        using var viewModel = new GettingStartedViewModel(store, coordinator, shell);

        Assert.Equal(0, viewModel.CourseCount);
        Assert.True(viewModel.CanStartInitialSetup);
        Assert.False(viewModel.HasCourses);

        await viewModel.StartInitialSetup.Execute();

        Assert.Equal(1, viewModel.CourseCount);
        Assert.Equal(1, viewModel.ClassCount);
        Assert.Equal(0, viewModel.StudentCount);
        Assert.Equal(0, viewModel.CriterionCount);
        Assert.False(viewModel.CanStartInitialSetup);
        Assert.True(viewModel.HasCourses);
        Assert.True(viewModel.HasClasses);
        Assert.False(viewModel.HasStudents);
        Assert.False(viewModel.HasCriteria);
        Assert.Equal("Configuración inicial lista: 1 curso.", viewModel.SetupStatus);
        Assert.Equal("Añade alumnos", viewModel.NextSetupStepTitle);
        Assert.Equal("Abrir alumnos", viewModel.NextSetupActionText);
        Assert.Equal("Classes", shell.RequestedSection);
        Assert.Equal(1, store.SaveCount);
        Assert.Same(store.Root.Courses.Single(), selection.SelectedCourse);
        Assert.Same(store.Root.Courses.Single().Classes.Single(), selection.SelectedClass);
    }

    [Theory]
    [MemberData(nameof(NextSetupCases))]
    public async Task Open_next_setup_step_routes_to_first_missing_step(Root root, string expectedTitle, string expectedAction, string expectedSection)
    {
        using var store = RecordingSchoolStore.FromDomain(root);
        var selection = new SchoolSelectionState();
        var shell = new RecordingShell();
        var coordinator = CreateCoordinator(store, selection, shell);
        using var viewModel = new GettingStartedViewModel(store, coordinator, shell);

        Assert.Equal(expectedTitle, viewModel.NextSetupStepTitle);
        Assert.Equal(expectedAction, viewModel.NextSetupActionText);

        await viewModel.OpenNextSetupStep.Execute();

        Assert.Equal(expectedSection, shell.RequestedSection);
    }

    [Fact]
    public void Sections_are_ordered_for_the_teacher_workflow()
    {
        var sections = new[]
            {
                typeof(GettingStartedViewModel),
                typeof(CoursesViewModel),
                typeof(ClassesViewModel),
                typeof(StudentsViewModel),
                typeof(CriteriaViewModel),
                typeof(GradesViewModel)
            }
            .Select(type => type.GetCustomAttribute<SectionAttribute>())
            .OrderBy(section => section!.SortIndex)
            .Select(section => section!.FriendlyName)
            .ToList();

        Assert.Equal(["Inicio", "Cursos", "Clases", "Alumnos", "Criterios", "Notas"], sections);
    }

    public static IEnumerable<object[]> NextSetupCases()
    {
        yield return [CreateRootWithCourseOnly(), "Añade clases", "Abrir clases", "Classes"];
        yield return [CreateRootWithClassOnly(), "Añade alumnos", "Abrir alumnos", "Students"];
        yield return [CreateRootWithStudentOnly(), "Define criterios", "Abrir criterios", "Criteria"];
        yield return [CreateReadyRoot(), "Preparado para evaluar", "Abrir notas", "Grades"];
    }

    private static InitialSetupCoordinator CreateCoordinator(IDynamicSchoolStore store, SchoolSelectionState selection, IShell shell)
    {
        return new InitialSetupCoordinator(
            store,
            new CompletingInitialSetupDialog(),
            new InitialSetupWizardFactory(),
            new InitialSetupApplicator(store, selection),
            shell,
            new NullNotificationService());
    }

    private static Root CreateRootWithCourseOnly()
    {
        return new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Name = "1º ESO",
                    Terms = [1]
                }
            ]
        };
    }

    private static Root CreateRootWithClassOnly()
    {
        var root = CreateRootWithCourseOnly();
        root.Courses[0].Classes.Add(new Class
        {
            Id = "class-1",
            Name = "A",
            Students = [],
            Assessments = []
        });
        return root;
    }

    private static Root CreateRootWithStudentOnly()
    {
        var root = CreateRootWithClassOnly();
        root.Courses[0].Classes[0].Students.Add(new Student
        {
            Id = "student-1",
            FirstName = "Ana",
            LastName = "García"
        });
        return root;
    }

    private static Root CreateReadyRoot()
    {
        var root = CreateRootWithStudentOnly();
        root.Courses[0].Criteria.Add(new Criterion
        {
            Id = "criterion-1",
            Name = "Trabajo diario",
            Weight = 1,
            ClassId = string.Empty,
            Term = 1
        });
        return root;
    }

    private sealed class CompletingInitialSetupDialog : IDialog
    {
        public async Task<bool> Show<TViewModel>(
            Maybe<TViewModel> viewModel,
            Maybe<IObservable<string>> title,
            Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
            Maybe<object> icon = default,
            DialogTone tone = DialogTone.Neutral,
            DialogSize size = DialogSize.Auto)
        {
            var wizard = Assert.IsType<GraphWizard<InitialSetupDraft>>(viewModel.Value);
            _ = optionsFactory(viewModel, new NoopCloseable()).ToList();

            var courses = Assert.IsType<InitialSetupCoursesStepViewModel>(wizard.CurrentStep!.Content);
            courses.Courses.Single(course => course.Name == "1º ESO").IsSelected = true;
            Assert.True(await wizard.Next.CanExecute.FirstAsync(canExecute => canExecute));
            await wizard.Next.Execute();

            var classes = Assert.IsType<InitialSetupClassesStepViewModel>(wizard.CurrentStep!.Content);
            classes.Courses.Single(course => course.CourseName == "1º ESO").Classes.Single(cls => cls.Name == "A").IsSelected = true;
            Assert.True(await wizard.Next.CanExecute.FirstAsync(canExecute => canExecute));
            await wizard.Next.Execute();

            return true;
        }
    }

    private sealed class NoopCloseable : ICloseable
    {
        public void Close()
        {
        }

        public void Dismiss()
        {
        }
    }

    private sealed class RecordingShell : IShell
    {
        public IEnumerable<ISection> Sections { get; } = [];

        public ReactiveProperty<ISection> SelectedSection { get; } = new((ISection)null!);

        public string? RequestedSection { get; private set; }

        public void GoToSection(string sectionId)
        {
            RequestedSection = sectionId;
        }
    }
}
