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
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Models;
using EvaluacionesApp.Desktop.ViewModels;

namespace EvaluacionesApp.Desktop.Views.Maintenance;

public partial class CriteriaViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<CourseCriterionCopyTarget> EmptyCourseCopyTargets = new(new ObservableCollection<CourseCriterionCopyTarget>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<CourseCriterionCopyTarget, string> courseCopyTargetsCache = new(target => target.CourseId);
    private readonly Dictionary<DynamicCourse, IDisposable> courseSubscriptions = new();
    private readonly Dictionary<DynamicCourse, CourseCriterionCopyTarget> courseCopyTargetsByCourse = new();
    private CompositeDisposable? courseAnchors;
    private DynamicRoot? root;
    private readonly NotifyCollectionChangedEventHandler termCollectionChanged;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    private ReadOnlyObservableCollection<CourseCriterionCopyTarget> courseCopyTargets = EmptyCourseCopyTargets;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private ScopedCriterionNode? selectedNode;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private int selectedTerm = 1;

    private readonly ObservableCollection<ScopedCriterionNode> criteriaInternal = new();

    public ReadOnlyObservableCollection<ScopedCriterionNode> Criteria { get; }

    public ReadOnlyObservableCollection<CourseCriterionCopyTarget> CourseCopyTargets => courseCopyTargets;

    public ObservableCollection<int> Terms { get; } = new();

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<CriterionCopyTarget?, Unit> CopyCriteria { get; }

    public CriteriaViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        Criteria = new ReadOnlyObservableCollection<ScopedCriterionNode>(criteriaInternal);
        termCollectionChanged = (_, _) =>
        {
            UpdateTerms(SelectedCourse);
            EnsureSelectedTermExists();
        };
        courseCopyTargetsCache.Connect()
            .OnItemRemoved(DisposeCourseCopyTarget)
            .AutoRefresh(target => target.CourseOrder)
            .AutoRefresh(target => target.CourseName)
            .Sort(SortExpressionComparer<CourseCriterionCopyTarget>
                .Ascending(target => target.CourseOrder)
                .ThenByAscending(target => target.CourseName))
            .Bind(out courseCopyTargets)
            .Subscribe()
            .DisposeWith(anchors);
        var hasCourse = this.WhenAnyValue(x => x.SelectedCourse, x => x.SelectedClass)
            .Select(tuple => tuple.Item1 != null && tuple.Item2 != null);
        var hasCriterion = this.WhenAnyValue(x => x.SelectedNode).Select(c => c != null);

        AddRootCriterion = ReactiveCommand.CreateFromTask(DoAddRootCriterion, hasCourse);
        AddChildCriterion = ReactiveCommand.CreateFromTask(DoAddChildCriterion, hasCriterion);
        DeleteCriterion = ReactiveCommand.CreateFromTask(DoDeleteCriterion, hasCriterion);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        CopyCriteria = ReactiveCommand.CreateFromTask<CriterionCopyTarget?>(DoCopyCriteria, hasCourse);
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

        this.WhenAnyValue(x => x.SelectedClass, x => x.SelectedTerm)
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
        if (courseSubscriptions.ContainsKey(course))
        {
            return;
        }

        var target = GetOrCreateCourseCopyTarget(course);

        foreach (var cls in course.Classes)
        {
            target.AddOrUpdateClass(cls);
        }

        var subscription = course.ClassesChanges.Subscribe(changes => HandleCourseClassChanges(course, changes));
        courseSubscriptions[course] = subscription;
    }

    void UnregisterCourse(DynamicCourse course)
    {
        if (courseSubscriptions.Remove(course, out var subscription))
        {
            subscription.Dispose();
        }

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
        return target;
    }

    void RemoveCourseCopyTarget(DynamicCourse course)
    {
        if (!courseCopyTargetsByCourse.Remove(course, out _))
        {
            return;
        }

        courseCopyTargetsCache.RemoveKey(course.Id);
    }

    void DisposeCourseCopyTarget(CourseCriterionCopyTarget target)
    {
        target.Dispose();
    }

    void HandleCourseClassChanges(DynamicCourse course, IChangeSet<DynamicClass, string> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                case ChangeReason.Update:
                    GetOrCreateCourseCopyTarget(course).AddOrUpdateClass(change.Current);
                    break;
                case ChangeReason.Remove:
                    RemoveClassFromCourseTarget(course, change.Current);
                    break;
            }
        }
    }

    void RemoveClassFromCourseTarget(DynamicCourse course, DynamicClass cls)
    {
        if (!courseCopyTargetsByCourse.TryGetValue(course, out var target))
        {
            return;
        }

        target.RemoveClass(cls);

        if (target.IsEmpty)
        {
            RemoveCourseCopyTarget(course);
        }
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

    async Task DoCopyCriteria(CriterionCopyTarget? target)
    {
        if (target == null || SelectedCourse == null || SelectedClass == null)
        {
            return;
        }

        var destinationCourse = target.Course;
        var destinationClass = target.Class;
        var destinationTerm = target.Term;

        var clones = Criteria
            .Select(node => CloneCriterion(node, destinationClass, destinationTerm))
            .ToList();

        var toRemove = destinationCourse.Criteria
            .Where(rootCriterion => rootCriterion.MatchesTreeScope(destinationClass.Id, destinationTerm))
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

    Models.Criterion CloneCriterion(ScopedCriterionNode node, DynamicClass destinationClass, int destinationTerm)
    {
        var criterion = node.Criterion;
        var classId = string.IsNullOrWhiteSpace(criterion.ClassId) ? string.Empty : destinationClass.Id;
        var term = criterion.Term.HasValue ? destinationTerm : criterion.Term;

        return new Models.Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = criterion.Name,
            Weight = criterion.Weight,
            ClassId = classId,
            Term = term,
            Children = node.Children
                .Select(child => CloneCriterion(child, destinationClass, destinationTerm))
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

    public void Dispose()
    {
        courseAnchors?.Dispose();
        anchors.Dispose();
        foreach (var subscription in courseSubscriptions.Values.ToList())
        {
            subscription.Dispose();
        }

        courseSubscriptions.Clear();

        foreach (var target in courseCopyTargetsCache.Items.ToList())
        {
            target.Dispose();
        }

        courseCopyTargetsCache.Dispose();
        courseCopyTargetsByCourse.Clear();
    }
}
