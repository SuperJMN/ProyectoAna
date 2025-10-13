using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;
using Zafiro.Avalonia.Dialogs;

namespace EvaluacionesApp.Desktop.Features.Criteria;

public partial class CriteriaViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<MenuViewModel> EmptyCopyMenuItems = new(new ObservableCollection<MenuViewModel>());

    private readonly DynamicSchoolStore store;
    private readonly IDialog dialogService;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<CourseCriterionCopyTarget, string> courseCopyTargetsCache = new(target => target.CourseId);
    private readonly SourceCache<MenuViewModel, string> copyMenuCache = new(menu => menu.Key);
    private readonly Dictionary<DynamicCourse, CourseCriterionCopyTarget> courseCopyTargetsByCourse = new();
    private readonly Dictionary<CourseCriterionCopyTarget, CourseCopyMenuViewModel> copyMenusByTarget = new();
    private CompositeDisposable? courseAnchors;
    private DynamicRoot? root;
    private readonly NotifyCollectionChangedEventHandler termCollectionChanged;
    private readonly IObservable<bool> canDeleteCriterion;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    private ReadOnlyObservableCollection<MenuViewModel> copyCriteriaMenu = EmptyCopyMenuItems;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private ScopedCriterionNode? selectedNode;
    [Reactive] private int selectedTerm = 1;

    private readonly ObservableCollection<ScopedCriterionNode> criteriaInternal = new();

    public ReadOnlyObservableCollection<ScopedCriterionNode> Criteria { get; }

    public ReadOnlyObservableCollection<MenuViewModel> CopyCriteriaMenu => copyCriteriaMenu;

    public ObservableCollection<int> Terms { get; } = new();

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<CriterionCopyTarget?, Unit> CopyCriteria { get; }

public CriteriaViewModel(DynamicSchoolStore store, IDialog dialogService)
{
    this.store = store;
    this.dialogService = dialogService;
    Criteria = new ReadOnlyObservableCollection<ScopedCriterionNode>(criteriaInternal);
    termCollectionChanged = (_, _) =>
    {
        UpdateTerms(SelectedCourse);
        EnsureSelectedTermExists();
    };
    var hasCourse = this.WhenAnyValue(x => x.SelectedCourse)
        .Select(course => course != null);
    var hasCriterion = this.WhenAnyValue(x => x.SelectedNode).Select(c => c != null);

    // Only allow deletion when the selected node is a leaf (no children)
    // Note: We don't check for assessments here because they are managed reactively
    // and the removal of the criterion will be handled by the persistence layer
    canDeleteCriterion = this.WhenAnyValue(x => x.SelectedNode)
        .Select(node =>
        {
            if (node?.Criterion == null)
            {
                return false;
            }
            
            // Only allow delete if it's a leaf (has no children)
            return node.Criterion.Children.Count == 0;
        })
        .DistinctUntilChanged()
        .ObserveOn(RxApp.MainThreadScheduler);

    AddRootCriterion = ReactiveCommand.CreateFromTask(DoAddRootCriterion, hasCourse);
    AddChildCriterion = ReactiveCommand.CreateFromTask(DoAddChildCriterion, hasCriterion);
    DeleteCriterion = ReactiveCommand.CreateFromTask(DoDeleteCriterion, canDeleteCriterion);
    Save = ReactiveCommand.CreateFromTask(ExecuteSave);
    CopyCriteria = ReactiveCommand.CreateFromTask<CriterionCopyTarget?>(DoCopyCriteria, hasCourse);
    copyMenuCache.Connect()
            .DisposeMany()
            .AutoRefresh(menu => ((CourseCopyMenuViewModel)menu).CourseOrder)
            .AutoRefresh(menu => menu.Header)
            .Sort(SortExpressionComparer<MenuViewModel>
                .Ascending(menu => ((CourseCopyMenuViewModel)menu).CourseOrder)
                .ThenByAscending(menu => menu.Header))
            .Bind(out copyCriteriaMenu)
            .Subscribe()
            .DisposeWith(anchors);
        _ = Load();
    }

    async Task Load()
    {
        root = await store.GetRoot();
        Courses = root.Courses;
        RegisterExistingCourses(root);

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedTerm)
            .Subscribe(_ => RefreshCriteria())
            .DisposeWith(anchors);

        root.CoursesChanges
            .Subscribe(HandleCoursesChanged)
            .DisposeWith(anchors);

        SelectedCourse = Courses.FirstOrDefault();
        UpdateTerms(SelectedCourse);
        EnsureSelectedTermExists();
    }

    void RegisterExistingCourses(DynamicRoot currentRoot)
    {
        foreach (var course in currentRoot.Courses)
        {
            RegisterCourse(course);
        }
    }

    void HandleCoursesChanged(IChangeSet<DynamicCourse, string> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    RegisterCourse(change.Current);
                    break;
                case ChangeReason.Remove:
                    UnregisterCourse(change.Current);
                    break;
            }
        }
    }

    void RegisterCourse(DynamicCourse course)
    {
        var target = GetOrCreateCourseCopyTarget(course);
    }

    void UnregisterCourse(DynamicCourse course)
    {
        RemoveCourseCopyTarget(course);
    }

    CourseCriterionCopyTarget GetOrCreateCourseCopyTarget(DynamicCourse course)
    {
        if (courseCopyTargetsByCourse.TryGetValue(course, out var existing))
        {
            return existing;
        }

        var target = new CourseCriterionCopyTarget(course);
        courseCopyTargetsByCourse[course] = target;
        courseCopyTargetsCache.AddOrUpdate(target);
        var menu = new CourseCopyMenuViewModel(target, CopyCriteria);
        copyMenusByTarget[target] = menu;
        copyMenuCache.AddOrUpdate(menu);
        return target;
    }

    void RemoveCourseCopyTarget(DynamicCourse course)
    {
        if (!courseCopyTargetsByCourse.Remove(course, out var target))
        {
            return;
        }

        RemoveCourseCopyMenu(target);
        courseCopyTargetsCache.RemoveKey(course.Id);
        target.Dispose();
    }

    void RemoveCourseCopyMenu(CourseCriterionCopyTarget target)
    {
        if (!copyMenusByTarget.Remove(target, out var menu))
        {
            return;
        }

        copyMenuCache.RemoveKey(menu.Key);
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;

        if (course == null)
        {
            Terms.Clear();
            criteriaInternal.Clear();
            SelectedNode = null;
            return;
        }

        courseAnchors = new CompositeDisposable();

        var termCollection = (INotifyCollectionChanged)course.Terms;
        termCollection.CollectionChanged += termCollectionChanged;
        courseAnchors.Add(Disposable.Create(() => termCollection.CollectionChanged -= termCollectionChanged));

        UpdateTerms(course);
        EnsureSelectedTermExists();

        course.CriteriaChanges
            .MergeManyChangeSets(c => c.SelfAndDescendants())
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Weight)
            .AutoRefresh(c => c.Id)
            .AutoRefresh(c => c.Term)
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(courseAnchors);

        course.CriteriaChanges
            .MergeManyChangeSets(c => c.SelfAndDescendants())
            .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
            .Subscribe(_ => RefreshCriteria())
            .DisposeWith(courseAnchors);

        RefreshCriteria();
        SelectedNode = Criteria.FirstOrDefault();
    }

    void UpdateTerms(DynamicCourse? course)
    {
        Terms.Clear();
        if (course == null)
        {
            return;
        }

        foreach (var term in course.Terms.Distinct().OrderBy(x => x))
        {
            Terms.Add(term);
        }

        if (Terms.Count == 0)
        {
            Terms.Add(1);
            Terms.Add(2);
            Terms.Add(3);
        }
    }

    void EnsureSelectedTermExists()
    {
        if (Terms.Count == 0)
        {
            SelectedTerm = 1;
            return;
        }

        if (!Terms.Contains(SelectedTerm))
        {
            SelectedTerm = Terms.First();
        }
    }

    void RefreshCriteria()
    {
        var previous = SelectedNode?.Criterion;
        criteriaInternal.Clear();

        if (SelectedCourse == null)
        {
            SelectedNode = null;
            return;
        }

        var relevant = SelectedCourse.FilterCriteriaTree(SelectedTerm)
            .Select(root => ScopedCriterionNode.Build(root, SelectedTerm))
            .Where(node => node != null)
            .Select(node => node!)
            .ToList();

        foreach (var node in relevant)
        {
            criteriaInternal.Add(node);
        }

        if (previous == null)
        {
            SelectedNode = Criteria.FirstOrDefault();
            return;
        }

        SelectedNode = FindNode(previous) ?? Criteria.FirstOrDefault();
    }

    async Task DoAddRootCriterion()
    {
        if (SelectedCourse == null)
        {
            return;
        }

        var idx = SelectedCourse.Criteria.Count + 1;
var model = new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Criterion {idx}",
            Weight = 1,
            ClassId = string.Empty,
            Term = SelectedTerm
        };
        var criterion = SelectedCourse.AddCriterion(model);
        RefreshCriteria();
        SelectedNode = FindNode(criterion) ?? SelectedNode;
        await ExecuteSave();
    }

    async Task DoAddChildCriterion()
    {
        if (SelectedNode?.Criterion == null)
        {
            return;
        }

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
        var child = parent.AddChild(model);
        RefreshCriteria();
        SelectedNode = FindNode(child) ?? SelectedNode;
        await ExecuteSave();
    }

async Task DoDeleteCriterion()
{
    if (SelectedCourse == null || SelectedNode?.Criterion == null)
    {
        return;
    }

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

        if (!confirmation.HasValue || !confirmation.Value)
        {
            return;
        }

        // Remove all assessments for this criterion from all classes
        foreach (var cls in SelectedCourse.Classes)
        {
            var toRemove = cls.Assessments
                .Where(a => a.CriterionId == criterion.Id)
                .ToList();

            foreach (var assessment in toRemove)
            {
                cls.RemoveAssessment(assessment);
            }
        }
    }

    var parent = criterion.Parent;
    if (parent != null)
    {
        parent.RemoveChild(criterion);
    }
    else
    {
        SelectedCourse.RemoveCriterion(criterion);
    }
    RefreshCriteria();
    SelectedNode = parent != null ? FindNode(parent) ?? Criteria.FirstOrDefault() : Criteria.FirstOrDefault();
    await ExecuteSave();
}

    async Task DoCopyCriteria(CriterionCopyTarget? target)
    {
        if (target == null || SelectedCourse == null)
        {
            return;
        }

        var destinationCourse = target.Course;
        var destinationTerm = target.Term;

        var clones = Criteria
            .Select(node => CloneCriterion(node, destinationTerm))
            .ToList();

        var toRemove = destinationCourse.Criteria
            .Where(rootCriterion => rootCriterion.MatchesTreeScope(destinationTerm))
            .ToList();

        foreach (var criterion in toRemove)
        {
            destinationCourse.RemoveCriterion(criterion);
        }

        foreach (var clone in clones)
        {
            destinationCourse.AddCriterion(clone);
        }

        await ExecuteSave();
    }

Criterion CloneCriterion(ScopedCriterionNode node, int destinationTerm)
    {
        var criterion = node.Criterion;
        var term = criterion.Term.HasValue ? destinationTerm : criterion.Term;

return new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = criterion.Name,
            Weight = criterion.Weight,
            ClassId = string.Empty,
            Term = term,
            Children = node.Children
                .Select(child => CloneCriterion(child, destinationTerm))
                .ToList()
        };
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    ScopedCriterionNode? FindNode(DynamicCriterion criterion)
    {
        foreach (var root in Criteria)
        {
            var match = root
                .SelfAndDescendants()
                .FirstOrDefault(node => ReferenceEquals(node.Criterion, criterion));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class CourseCopyMenuViewModel : MenuViewModel, IDisposable
    {
        private readonly CompositeDisposable anchors = new();
        private int courseOrder;

        public CourseCopyMenuViewModel(CourseCriterionCopyTarget target, ReactiveCommand<CriterionCopyTarget?, Unit> command)
        {
            Key = target.CourseId;
            Header = target.CourseName;
            CourseOrder = target.CourseOrder;

            target.WhenAnyValue(x => x.CourseName)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(value => Header = value)
                .DisposeWith(anchors);

            target.WhenAnyValue(x => x.CourseOrder)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(value => CourseOrder = value)
                .DisposeWith(anchors);

            target.Terms
                .ToObservableChangeSet()
                .Select(changes => changes.Transform(term => (MenuItemViewModel)new TermCopyMenuItemViewModel(term, command)))
                .DisposeMany()
                .Bind(out var termItems)
                .Subscribe()
                .DisposeWith(anchors);

            SetChildren(termItems);
        }

        public int CourseOrder
        {
            get => courseOrder;
            private set => this.RaiseAndSetIfChanged(ref courseOrder, value);
        }

        public void Dispose()
        {
            anchors.Dispose();
        }
    }

    private sealed class TermCopyMenuItemViewModel : MenuItemViewModel, IDisposable
    {
        public TermCopyMenuItemViewModel(TermCriterionCopyTarget term, ReactiveCommand<CriterionCopyTarget?, Unit> command)
        {
            Key = $"{term.Target.Course.Id}:{term.Target.Term}";
            Header = term.TermName;
            Command = command;
            CommandParameter = term.Target;
        }

        public void Dispose()
        {
        }
    }

    public void Dispose()
    {
        courseAnchors?.Dispose();
        anchors.Dispose();
        foreach (var target in courseCopyTargetsCache.Items.ToList())
        {
            target.Dispose();
        }

        courseCopyTargetsCache.Dispose();
        courseCopyTargetsByCourse.Clear();
        copyMenuCache.Dispose();
        copyMenusByTarget.Clear();
    }
}
