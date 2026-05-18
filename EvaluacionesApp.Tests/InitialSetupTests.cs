using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.InitialSetup;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Tests.Support;
using Reactive.Bindings;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;
using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace EvaluacionesApp.Tests;

public sealed class InitialSetupTests
{
    [Fact]
    public async Task Initial_setup_wizard_exposes_navigation_state_for_the_view()
    {
        var wizard = new InitialSetupWizardFactory().Create();

        Assert.Equal("Cursos", await wizard.CurrentStep!.Title.FirstAsync());
        Assert.Equal("Siguiente", await wizard.NextTitle.FirstAsync());
        Assert.False(await wizard.Next.CanExecute.FirstAsync(canExecute => !canExecute));
        Assert.False(await wizard.Back.CanExecute.FirstAsync(canExecute => !canExecute));

        var courses = Assert.IsType<InitialSetupCoursesStepViewModel>(wizard.CurrentStep.Content);
        courses.Courses.Single(course => course.Name == "1º ESO").IsSelected = true;
        courses.Courses.Single(course => course.Name == "2º Bachillerato").IsSelected = true;

        Assert.True(await wizard.Next.CanExecute.FirstAsync(canExecute => canExecute));

        await wizard.Next.Execute();

        var classes = Assert.IsType<InitialSetupClassesStepViewModel>(wizard.CurrentStep!.Content);
        Assert.Equal("Clases", await wizard.CurrentStep.Title.FirstAsync());
        Assert.True(await wizard.Back.CanExecute.FirstAsync(canExecute => canExecute));
        Assert.False(await wizard.Next.CanExecute.FirstAsync(canExecute => !canExecute));

        classes.Courses.Single(course => course.CourseName == "1º ESO").Classes.Single(cls => cls.Name == "A").IsSelected = true;
        classes.Courses.Single(course => course.CourseName == "2º Bachillerato").Classes.Single(cls => cls.Name == "B").IsSelected = true;

        Assert.True(await wizard.Next.CanExecute.FirstAsync(canExecute => canExecute));
    }

    [Fact]
    public void Courses_step_exposes_the_expected_presets()
    {
        var courses = new InitialSetupCoursesStepViewModel();

        Assert.Equal(
            ["1º ESO", "2º ESO", "3º ESO", "4º ESO", "1º Bachillerato", "2º Bachillerato"],
            courses.Courses.Select(course => course.Name));
    }

    [Fact]
    public async Task Initial_setup_creates_selected_courses_and_classes_without_students_and_saves_once()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        var selection = new SchoolSelectionState();
        var applicator = new InitialSetupApplicator(store, selection);

        var result = await applicator.Apply(new InitialSetupDraft([
            new InitialSetupCourseClassesDraft("1º ESO", 1, ["A", "B"]),
            new InitialSetupCourseClassesDraft("2º Bachillerato", 2, ["C"])
        ]));

        Assert.Collection(
            store.Root.Courses,
            course =>
            {
                Assert.Equal("1º ESO", course.Name);
                Assert.Equal(1, course.Number);
                Assert.Equal([1, 2, 3], course.Terms);
                Assert.Equal(["A", "B"], course.Classes.Select(cls => cls.Name));
                Assert.All(course.Classes, cls => Assert.Empty(cls.Students));
                Assert.Empty(course.Criteria);
            },
            course =>
            {
                Assert.Equal("2º Bachillerato", course.Name);
                Assert.Equal(2, course.Number);
                Assert.Equal([1, 2, 3], course.Terms);
                var cls = Assert.Single(course.Classes);
                Assert.Equal("C", cls.Name);
                Assert.Empty(cls.Students);
                Assert.Empty(course.Criteria);
            });

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(2, result.Courses.Count);
        Assert.Same(result.SelectedCourse, selection.SelectedCourse);
        Assert.Same(result.SelectedClass, selection.SelectedClass);
        Assert.Equal("1º ESO", selection.SelectedCourse?.Name);
        Assert.Equal("A", selection.SelectedClass?.Name);
    }

    [Fact]
    public async Task Cancelled_initial_setup_does_not_mutate_or_save()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root());
        var selection = new SchoolSelectionState();
        var coordinator = new InitialSetupCoordinator(
            store,
            new CancellingDialog(),
            new InitialSetupWizardFactory(),
            new InitialSetupApplicator(store, selection),
            new RecordingShell(),
            new NullNotificationService());

        await coordinator.RunIfNeeded();

        Assert.Empty(store.Root.Courses);
        Assert.Equal(0, store.SaveCount);
        Assert.Null(selection.SelectedCourse);
        Assert.Null(selection.SelectedClass);
    }

    private sealed class CancellingDialog : IDialog
    {
        public Task<bool> Show<TViewModel>(
            Maybe<TViewModel> viewModel,
            Maybe<IObservable<string>> title,
            Func<Maybe<TViewModel>, ICloseable, IEnumerable<IOption>> optionsFactory,
            Maybe<object> icon = default,
            DialogTone tone = DialogTone.Neutral,
            DialogSize size = DialogSize.Auto)
        {
            return Task.FromResult(false);
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
