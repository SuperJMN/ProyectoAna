using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.Criteria.Operations;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;

namespace EvaluacionesApp.Desktop.Features.Criteria;

public partial class CriteriaViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly IDialog dialogService;
    private readonly DynamicSchoolStore store;
    private readonly CriteriaCopyFeatureViewModel copyFeature;
    private readonly DynamicRoot root;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private ScopedCriterionNode? selectedNode;
    [Reactive] private int selectedTerm = 1;

    public CriteriaViewModel(DynamicSchoolStore store, IDialog dialogService)
    {
        this.store = store;
        this.dialogService = dialogService;
        root = store.Root;

        // Reactive setup for courses
        root.Courses.ToObservableChangeSet()
            .Bind(out var courses)
            .Subscribe()
            .DisposeWith(anchors);
        Courses = courses;

        // Register/unregister courses for copy feature
        root.CoursesChanges
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ChangeReason.Add)
                        copyFeature?.RegisterCourse(change.Current);
                    else if (change.Reason == ChangeReason.Remove)
                        copyFeature?.UnregisterCourse(change.Current);
                }
            })
            .DisposeWith(anchors);

        // Reactive pipeline for criteria based on selected course and term
        var criteriaCache = new SourceCache<ScopedCriterionNode, string>(node => node.Id);
        criteriaCache.Connect()
            .Bind(out var criteria)
            .Subscribe()
            .DisposeWith(anchors);
        Criteria = criteria;

        var criteriaSource = this.WhenAnyValue(
                x => x.SelectedCourse,
                x => x.SelectedTerm,
                (course, term) => (course, term))
            .Select(tuple =>
            {
                var (course, term) = tuple;
                if (course == null)
                    return Observable.Return(Array.Empty<ScopedCriterionNode>());

                return course.CriteriaChanges
                    .MergeManyChangeSets(c => c.SelfAndDescendants())
                    .AutoRefresh(c => c.Name)
                    .AutoRefresh(c => c.Weight)
                    .AutoRefresh(c => c.Id)
                    .AutoRefresh(c => c.Term)
                    .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                    .Select(_ => course.FilterCriteriaTree(term)
                        .Select(root => ScopedCriterionNode.Build(root, term))
                        .Where(node => node != null)
                        .Select(node => node!)
                        .ToArray());
            })
            .Switch();

        criteriaSource
            .Subscribe(nodes =>
            {
                var previous = SelectedNode?.Criterion;
                criteriaCache.Edit(updater =>
                {
                    updater.Clear();
                    updater.AddOrUpdate(nodes);
                });

                // Try to restore previous selection
                if (previous != null)
                {
                    var found = criteria.SelectMany(n => n.SelfAndDescendants())
                        .FirstOrDefault(n => ReferenceEquals(n.Criterion, previous));
                    SelectedNode = found ?? criteria.FirstOrDefault();
                }
                else
                {
                    SelectedNode = criteria.FirstOrDefault();
                }
            })
            .DisposeWith(anchors);

        // Reactive pipeline for terms based on selected course
        var termsCache = new SourceCache<int, int>(term => term);
        termsCache.Connect()
            .Sort(SortExpressionComparer<int>.Ascending(x => x))
            .Bind(out var termsCollection)
            .Subscribe()
            .DisposeWith(anchors);
        Terms = termsCollection;

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(course =>
            {
                var terms = course?.Terms ?? (IEnumerable<int>)new[] { 1, 2, 3 };
                termsCache.Edit(updater =>
                {
                    updater.Clear();
                    updater.AddOrUpdate(terms);
                });

                // Ensure selected term exists in the new terms collection
                if (!terms.Contains(SelectedTerm))
                {
                    SelectedTerm = terms.FirstOrDefault();
                }
            })
            .DisposeWith(anchors);

        // Auto-save when criteria change
        criteriaSource
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(ReactiveCommand.CreateFromTask(ExecuteSave))
            .DisposeWith(anchors);

        var hasCourse = this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null);
        var hasCriterion = this.WhenAnyValue(x => x.SelectedNode).Select(c => c != null);

        var canDeleteCriterion = this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node?.Criterion?.Children.Count == 0)
            .DistinctUntilChanged();

        AddRootCriterion = ReactiveCommand.CreateFromTask(DoAddRootCriterion, hasCourse);
        AddChildCriterion = ReactiveCommand.CreateFromTask(DoAddChildCriterion, hasCriterion);
        DeleteCriterion = ReactiveCommand.CreateFromTask(DoDeleteCriterion, canDeleteCriterion);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);

        var criteriaObservable = this.WhenAnyValue(x => x.Criteria)
            .Select(c => (IReadOnlyList<ScopedCriterionNode>)c);
        var selectedCourseObservable = this.WhenAnyValue(x => x.SelectedCourse);

        copyFeature = new CriteriaCopyFeatureViewModel(store, criteriaObservable, selectedCourseObservable);
        copyFeature.DisposeWith(anchors);

        // Register existing courses
        foreach (var course in root.Courses)
            copyFeature.RegisterCourse(course);

        // Set initial selection
        SelectedCourse = Courses.FirstOrDefault();
    }

    public ReadOnlyObservableCollection<ScopedCriterionNode> Criteria { get; }

    public ReadOnlyObservableCollection<DynamicCourse> Courses { get; }

    public ReadOnlyObservableCollection<int> Terms { get; }

    public CriteriaCopyFeatureViewModel CopyFeature => copyFeature;

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public void Dispose()
    {
        anchors.Dispose();
    }

    private async Task DoAddRootCriterion()
    {
        if (SelectedCourse == null) return;

        var idx = SelectedCourse.Criteria.Count + 1;
        var model = new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Criterion {idx}",
            Weight = 1,
            ClassId = string.Empty,
            Term = SelectedTerm
        };
        SelectedCourse.AddCriterion(model);
        await ExecuteSave();
    }

    private async Task DoAddChildCriterion()
    {
        if (SelectedNode?.Criterion == null) return;

        var parent = SelectedNode.Criterion;
        var idx = parent.Children.Count + 1;
        var model = new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Subcriterion {idx}",
            Weight = 1,
            ClassId = string.IsNullOrWhiteSpace(parent.ClassId) ? string.Empty : parent.ClassId,
            Term = parent.Term ?? SelectedTerm
        };
        parent.AddChild(model);
        await ExecuteSave();
    }

    private async Task DoDeleteCriterion()
    {
        if (SelectedCourse == null || SelectedNode?.Criterion == null) return;

        var criterion = SelectedNode.Criterion;

        // Check if there are assessments (scores) referencing this criterion
        var assessmentsWithScores = SelectedCourse.Classes
            .SelectMany(c => c.Assessments)
            .Where(a => a.CriterionId == criterion.Id && a.Score.HasValue)
            .ToList();

        if (assessmentsWithScores.Any())
        {
            var count = assessmentsWithScores.Count;
            var confirmation = await dialogService.ShowConfirmation(
                "Confirmar borrado",
                $"Este criterio tiene {count} valoración(es) asociada(s). ¿Deseas borrar el criterio y todas sus valoraciones?",
                "Sí, borrar",
                "Cancelar");

            if (!confirmation.HasValue || !confirmation.Value) return;

            // Remove all assessments for this criterion from all classes
            foreach (var cls in SelectedCourse.Classes)
            {
                var toRemove = cls.Assessments
                    .Where(a => a.CriterionId == criterion.Id)
                    .ToList();

                foreach (var assessment in toRemove)
                    cls.RemoveAssessment(assessment);
            }
        }

        var parent = criterion.Parent;
        if (parent != null)
            parent.RemoveChild(criterion);
        else
            SelectedCourse.RemoveCriterion(criterion);

        SelectedNode = null;
        await ExecuteSave();
    }

    private async Task ExecuteSave()
    {
        await store.SaveAsync();
    }
}
