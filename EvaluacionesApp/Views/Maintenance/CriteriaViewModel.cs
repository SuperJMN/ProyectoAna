using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Dynamic;
using EvaluacionesApp.ViewModels;
using System.Threading.Tasks;

namespace EvaluacionesApp.Views.Maintenance;

public partial class CriteriaViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private CompositeDisposable? courseAnchors;
    private DynamicRoot? root;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private ScopedCriterionNode? selectedNode;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private int selectedTerm = 1;

    private readonly ObservableCollection<ScopedCriterionNode> criteriaInternal = new();

    public ReadOnlyObservableCollection<ScopedCriterionNode> Criteria { get; }

    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public CriteriaViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        Criteria = new ReadOnlyObservableCollection<ScopedCriterionNode>(criteriaInternal);
        var hasCourse = this.WhenAnyValue(x => x.SelectedCourse, x => x.SelectedClass)
            .Select(tuple => tuple.Item1 != null && tuple.Item2 != null);
        var hasCriterion = this.WhenAnyValue(x => x.SelectedNode).Select(c => c != null);

        AddRootCriterion = ReactiveCommand.CreateFromTask(DoAddRootCriterion, hasCourse);
        AddChildCriterion = ReactiveCommand.CreateFromTask(DoAddChildCriterion, hasCriterion);
        DeleteCriterion = ReactiveCommand.CreateFromTask(DoDeleteCriterion, hasCriterion);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        _ = Load();
    }

    async Task Load()
    {
        root = await store.GetRoot();
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
        SelectedTerm = 1;
        RefreshCriteria();
        SelectedNode = Criteria.FirstOrDefault();

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass, x => x.SelectedTerm)
            .Subscribe(_ => RefreshCriteria())
            .DisposeWith(anchors);
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;

        if (course == null)
        {
            criteriaInternal.Clear();
            SelectedNode = null;
            return;
        }

        courseAnchors = new CompositeDisposable();

        SelectedClass = course.Classes.FirstOrDefault();
        course.CriteriaChanges
            .MergeManyChangeSets(c => c.SelfAndDescendants())
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Weight)
            .AutoRefresh(c => c.Id)
            .AutoRefresh(c => c.ClassId)
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

    void RefreshCriteria()
    {
        var previous = SelectedNode?.Criterion;
        criteriaInternal.Clear();

        if (SelectedCourse == null || SelectedClass == null)
        {
            SelectedNode = null;
            return;
        }

        var relevant = SelectedCourse.FilterCriteriaTree(SelectedClass.Id, SelectedTerm)
            .Select(root => ScopedCriterionNode.Build(root, SelectedClass.Id, SelectedTerm))
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
        if (SelectedCourse == null || SelectedClass == null)
        {
            return;
        }

        var idx = SelectedCourse.Criteria.Count + 1;
        var model = new Models.Criterion
        {
            Id = $"C{idx}",
            Name = $"Criterion {idx}",
            Weight = 1,
            ClassId = SelectedClass?.Id ?? string.Empty,
            Term = SelectedTerm
        };
        var criterion = SelectedCourse.AddCriterion(model);
        RefreshCriteria();
        SelectedNode = FindNode(criterion) ?? SelectedNode;
        await ExecuteSave();
    }

    async Task DoAddChildCriterion()
    {
        if (SelectedNode?.Criterion == null || SelectedClass == null)
        {
            return;
        }

        var parent = SelectedNode.Criterion;
        var idx = parent.Children.Count + 1;
        var model = new Models.Criterion
        {
            Id = $"{parent.Id}.{idx}",
            Name = $"Subcriterion {idx}",
            Weight = 1,
            ClassId = string.IsNullOrWhiteSpace(parent.ClassId) ? SelectedClass?.Id ?? string.Empty : parent.ClassId,
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
        if (criterion.Children.Any())
        {
            return;
        }

        var effectiveTerm = criterion.EffectiveTerm ?? 1;
        var hasAssessments = SelectedCourse.Classes
            .Where(c => string.IsNullOrWhiteSpace(criterion.ClassId) || c.Id == criterion.ClassId)
            .SelectMany(c => c.Assessments)
            .Any(a => a.CriterionId == criterion.Id && (a.Term ?? 1) == effectiveTerm);
        if (hasAssessments)
        {
            return;
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

    public void Dispose()
    {
        courseAnchors?.Dispose();
        anchors.Dispose();
    }
}
