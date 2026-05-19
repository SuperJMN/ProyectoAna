using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
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
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.Criteria;

[Section(name: "Criteria", icon: "mdi-format-list-bulleted", sortIndex: 5, FriendlyName = "Criterios")]
public partial class CriteriaViewModel : ReactiveValidationObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly IDialog dialogService;
    private readonly IDynamicSchoolStore store;
    private readonly INotificationService notifications;
    private readonly CriteriaCopyFeatureViewModel copyFeature;
    private readonly DynamicRoot root;
    private readonly IScheduler scheduler;
    private string? criterionIdToSelectAfterRebuild;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private ScopedCriterionNode? selectedNode;
    [Reactive] private int selectedTerm = 1;
    private bool selectedCriterionNameValid = true;
    private bool selectedCriterionWeightValid = true;

    public CriteriaViewModel(
        IDynamicSchoolStore store,
        IDialog dialogService,
        INotificationService notifications,
        IScheduler? scheduler = null,
        TimeSpan? criteriaRefreshInterval = null,
        TimeSpan? autoSaveInterval = null)
    {
        this.store = store;
        this.dialogService = dialogService;
        this.notifications = notifications;
        this.scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;
        var refreshInterval = criteriaRefreshInterval ?? TimeSpan.FromMilliseconds(100);
        var saveInterval = autoSaveInterval ?? TimeSpan.FromMilliseconds(400);
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

        // Reactive pipeline for criteria - rebuild when course, term, or criteria change
        var criteriaCollection = new ObservableCollection<ScopedCriterionNode>();
        Criteria = new ReadOnlyObservableCollection<ScopedCriterionNode>(criteriaCollection);

        void RebuildCriteria(DynamicCourse? course, int term)
        {
            var selectionId = criterionIdToSelectAfterRebuild ?? SelectedNode?.Criterion.Id;
            criterionIdToSelectAfterRebuild = null;
            criteriaCollection.Clear();
            if (course != null)
            {
                var nodes = course.FilterCriteriaTree(term)
                    .Select(root => ScopedCriterionNode.Build(root, term))
                    .OfType<ScopedCriterionNode>();
                foreach (var node in nodes)
                    criteriaCollection.Add(node);
            }

            SelectedNode = selectionId == null
                ? criteriaCollection.FirstOrDefault()
                : FindNode(criteriaCollection, selectionId) ?? criteriaCollection.FirstOrDefault();

            RaiseCriteriaActionStateChanged();
            RaiseSelectedCriterionStateChanged();
        }

        // Rebuild when course or term changes
        this.WhenAnyValue(x => x.SelectedCourse, x => x.SelectedTerm)
            .ObserveOn(this.scheduler)
            .Subscribe(t => RebuildCriteria(t.Item1, t.Item2))
            .DisposeWith(anchors);

        // Also rebuild when criteria in the selected course change
        this.WhenAnyValue(x => x.SelectedCourse)
            .Select(course => course?.CriteriaChanges
                .MergeManyChangeSets(c => c.SelfAndDescendants())
                .Throttle(refreshInterval, this.scheduler)
                .Select(_ => Unit.Default) ?? Observable.Empty<Unit>())
            .Switch()
            .ObserveOn(this.scheduler)
            .Subscribe(_ => RebuildCriteria(SelectedCourse, SelectedTerm))
            .DisposeWith(anchors);

        // Auto-select first criterion when collection changes
        this.WhenAnyValue(x => x.Criteria.Count)
            .Where(count => count > 0 && SelectedNode == null)
            .Subscribe(_ => SelectedNode = Criteria.FirstOrDefault())
            .DisposeWith(anchors);

        // Auto-save when criteria in selected course change
        this.WhenAnyValue(x => x.SelectedCourse)
            .Select(course => course?.CriteriaChanges
                .MergeManyChangeSets(c => c.SelfAndDescendants())
                .AutoRefresh(c => c.Name)
                .AutoRefresh(c => c.Weight)
                .AutoRefresh(c => c.Term)
                .Throttle(saveInterval, this.scheduler)
                .Select(_ => Unit.Default) ?? Observable.Empty<Unit>())
            .Switch()
            .InvokeCommand(ReactiveCommand.CreateFromTask(ExecuteSave))
            .DisposeWith(anchors);

        // Reactive pipeline for terms - just bind directly to course terms
        var termsCollection = new ObservableCollection<int>();
        Terms = new ReadOnlyObservableCollection<int>(termsCollection);

        this.WhenAnyValue(x => x.SelectedCourse)
            .ObserveOn(this.scheduler)
            .Subscribe(course =>
            {
                var terms = (course?.Terms ?? (IEnumerable<int>)new[] { 1, 2, 3 }).OrderBy(x => x);
                termsCollection.Clear();
                foreach (var term in terms)
                    termsCollection.Add(term);

                // Ensure selected term exists
                if (!termsCollection.Contains(SelectedTerm))
                    SelectedTerm = termsCollection.FirstOrDefault();
            })
            .DisposeWith(anchors);

        var hasCourse = this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null);
        var selectedCriterionStateChanged = this.WhenAnyValue(x => x.SelectedNode)
            .Select(_ => Unit.Default)
            .Merge(this.WhenAnyValue(x => x.SelectedCourse)
                .Select(course => course?.CriteriaChanges
                    .MergeManyChangeSets(c => c.SelfAndDescendants())
                    .Select(_ => Unit.Default) ?? Observable.Empty<Unit>())
                .Switch())
            .ObserveOn(this.scheduler);

        selectedCriterionStateChanged
            .Subscribe(_ => RaiseSelectedCriterionStateChanged())
            .DisposeWith(anchors);

        var hasCriterion = selectedCriterionStateChanged
            .Select(_ => HasSelectedCriterion)
            .StartWith(HasSelectedCriterion)
            .DistinctUntilChanged();

        var canDeleteCriterion = selectedCriterionStateChanged
            .Select(_ => SelectedCriterionCanBeDeleted)
            .StartWith(SelectedCriterionCanBeDeleted)
            .DistinctUntilChanged();

        AddRootCriterion = ReactiveCommand.CreateFromTask(DoAddRootCriterion, hasCourse);
        AddChildCriterion = ReactiveCommand.CreateFromTask(DoAddChildCriterion, hasCriterion);
        AddChildCriterionToNode = ReactiveCommand.CreateFromTask<ScopedCriterionNode>(DoAddChildCriterion, hasCourse);
        DeleteCriterion = ReactiveCommand.CreateFromTask(DoDeleteCriterion, canDeleteCriterion);
        this.ValidationRule(x => x.SelectedCriterionNameValid, isValid => isValid, "El nombre del criterio no puede estar vacio");
        this.ValidationRule(x => x.SelectedCriterionWeightValid, isValid => isValid, "El peso del criterio no puede ser negativo");
        ObserveSelectedCriterionChanges()
            .Subscribe(UpdateSelectedCriterionValidation)
            .DisposeWith(anchors);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave, ValidationContext.Valid);
        Save.ThrownExceptions
            .Subscribe(ex => _ = this.notifications.Show("No se pudieron guardar los criterios", ex.Message))
            .DisposeWith(anchors);

        var criteriaObservable = this.WhenAnyValue(x => x.Criteria)
            .Select(c => (IReadOnlyList<ScopedCriterionNode>)c);
        var selectedCourseObservable = this.WhenAnyValue(x => x.SelectedCourse);
        var selectedTermObservable = this.WhenAnyValue(x => x.SelectedTerm);

        copyFeature = new CriteriaCopyFeatureViewModel(store, criteriaObservable, selectedCourseObservable, selectedTermObservable);
        copyFeature.DisposeWith(anchors);
        copyFeature.WhenAnyValue(x => x.HasCopyCriteriaTargets)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasCopyCriteriaTargets));
                RaiseCriteriaActionStateChanged();
            })
            .DisposeWith(anchors);

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

    public bool HasNoCriteriaForSelectedTerm => SelectedCourse != null && Criteria.Count == 0;

    public bool ShowAddRootCriterionAction => SelectedCourse != null;

    public bool HasCopyCriteriaTargets => copyFeature.HasCopyCriteriaTargets;

    public bool ShowCopyCriteriaAction => SelectedCourse != null && !HasNoCriteriaForSelectedTerm && HasCopyCriteriaTargets;

    public bool HasSelectedCriterion => SelectedNode?.Criterion != null;

    public bool ShowAddChildCriterionAction => HasSelectedCriterion;

    public bool SelectedCriterionHasChildren => SelectedNode?.Criterion.Children.Count > 0;

    public bool SelectedCriterionCanBeDeleted => SelectedNode?.Criterion.Children.Count == 0;

    public bool ShowDeleteCriterionAction => SelectedCriterionCanBeDeleted;

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<ScopedCriterionNode, Unit> AddChildCriterionToNode { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public bool SelectedCriterionNameValid
    {
        get => selectedCriterionNameValid;
        private set => this.RaiseAndSetIfChanged(ref selectedCriterionNameValid, value);
    }

    public bool SelectedCriterionWeightValid
    {
        get => selectedCriterionWeightValid;
        private set => this.RaiseAndSetIfChanged(ref selectedCriterionWeightValid, value);
    }

    public new void Dispose()
    {
        anchors.Dispose();
        base.Dispose();
    }

    private async Task DoAddRootCriterion()
    {
        if (SelectedCourse == null) return;

        var idx = SelectedCourse.Criteria.Count + 1;
        var model = new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Criterio {idx}",
            Weight = 1,
            ClassId = string.Empty,
            Term = SelectedTerm
        };
        criterionIdToSelectAfterRebuild = model.Id;
        SelectedCourse.AddCriterion(model);
        await ExecuteSave();
    }

    private Task DoAddChildCriterion()
    {
        return DoAddChildCriterion(SelectedNode);
    }

    private async Task DoAddChildCriterion(ScopedCriterionNode? selectedNode)
    {
        if (SelectedCourse == null || selectedNode?.Criterion == null) return;

        var parent = selectedNode.Criterion;
        if (selectedNode.Children.Count == 0)
        {
            var assessments = FindCriterionAssessments(SelectedCourse, parent);
            var scoredCount = assessments.Count(assessment => assessment.Score.HasValue);
            if (scoredCount > 0)
            {
                var confirmation = await dialogService.ShowConfirmation(
                    "Convertir en criterio calculado",
                    $"Este criterio tiene {scoredCount} valoración(es) directa(s). Al añadir subcriterios, esas valoraciones se eliminarán porque el criterio pasará a calcularse desde sus descendientes. ¿Deseas continuar?",
                    "Sí, añadir",
                    "Cancelar");

                if (!confirmation.HasValue || !confirmation.Value) return;
            }

            RemoveCriterionAssessments(SelectedCourse, parent);
        }

        var idx = parent.Children.Count + 1;
        var model = new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Subcriterio {idx}",
            Weight = 1,
            ClassId = string.IsNullOrWhiteSpace(parent.ClassId) ? string.Empty : parent.ClassId,
            Term = parent.Term ?? SelectedTerm
        };
        criterionIdToSelectAfterRebuild = model.Id;
        parent.AddChild(model);
        await ExecuteSave();
    }

    static List<DynamicAssessment> FindCriterionAssessments(DynamicCourse course, DynamicCriterion criterion)
    {
        return course.Classes
            .SelectMany(cls => cls.Assessments)
            .Where(assessment => assessment.CriterionId == criterion.Id)
            .ToList();
    }

    static void RemoveCriterionAssessments(DynamicCourse course, DynamicCriterion criterion)
    {
        foreach (var cls in course.Classes)
        {
            var toRemove = cls.Assessments
                .Where(assessment => assessment.CriterionId == criterion.Id)
                .ToList();

            foreach (var assessment in toRemove)
            {
                cls.RemoveAssessment(assessment);
            }
        }
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
        UpdateSelectedCriterionValidation(SelectedNode?.Criterion);
        if (HasErrors)
        {
            return;
        }

        await store.SaveAsync();
    }

    IObservable<DynamicCriterion?> ObserveSelectedCriterionChanges()
    {
        return this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node == null
                ? Observable.Return<DynamicCriterion?>(null)
                : node.Criterion.Changed
                    .Select(_ => (DynamicCriterion?)node.Criterion)
                    .StartWith(node.Criterion))
            .Switch();
    }

    void UpdateSelectedCriterionValidation(DynamicCriterion? criterion)
    {
        SelectedCriterionNameValid = criterion == null || HasText(criterion.Name);
        SelectedCriterionWeightValid = criterion == null || criterion.Weight >= 0m;
    }

    void RaiseSelectedCriterionStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasSelectedCriterion));
        this.RaisePropertyChanged(nameof(ShowAddChildCriterionAction));
        this.RaisePropertyChanged(nameof(SelectedCriterionHasChildren));
        this.RaisePropertyChanged(nameof(SelectedCriterionCanBeDeleted));
        this.RaisePropertyChanged(nameof(ShowDeleteCriterionAction));
    }

    void RaiseCriteriaActionStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasNoCriteriaForSelectedTerm));
        this.RaisePropertyChanged(nameof(ShowAddRootCriterionAction));
        this.RaisePropertyChanged(nameof(HasCopyCriteriaTargets));
        this.RaisePropertyChanged(nameof(ShowCopyCriteriaAction));
    }

    static ScopedCriterionNode? FindNode(IEnumerable<ScopedCriterionNode> nodes, string criterionId)
    {
        return nodes
            .SelectMany(node => node.SelfAndDescendants())
            .FirstOrDefault(node => node.Criterion.Id == criterionId);
    }

    static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}
