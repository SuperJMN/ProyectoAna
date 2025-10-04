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
    [Reactive] private DynamicCriterion? selectedCriterion;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private int selectedTerm = 1;

    private readonly ObservableCollection<DynamicCriterion> criteriaInternal = new();

    public ReadOnlyObservableCollection<DynamicCriterion> Criteria { get; }

    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public CriteriaViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        Criteria = new ReadOnlyObservableCollection<DynamicCriterion>(criteriaInternal);
        var hasCourse = this.WhenAnyValue(x => x.SelectedCourse, x => x.SelectedClass)
            .Select(tuple => tuple.Item1 != null && tuple.Item2 != null);
        var hasCriterion = this.WhenAnyValue(x => x.SelectedCriterion).Select(c => c != null);

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
        SelectedCriterion = Criteria.FirstOrDefault();

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
            SelectedCriterion = null;
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
        SelectedCriterion = Criteria.FirstOrDefault();
    }

    void RefreshCriteria()
    {
        criteriaInternal.Clear();

        if (SelectedCourse == null || SelectedClass == null)
        {
            SelectedCriterion = null;
            return;
        }

        var relevant = SelectedCourse.FilterCriteriaTree(SelectedClass.Id, SelectedTerm).ToList();
        foreach (var criterion in relevant)
        {
            criteriaInternal.Add(criterion);
        }

        if (!Criteria.Contains(SelectedCriterion))
        {
            SelectedCriterion = Criteria.FirstOrDefault();
        }
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
        SelectedCriterion = criterion;
        await ExecuteSave();
    }

    async Task DoAddChildCriterion()
    {
        if (SelectedCriterion == null || SelectedClass == null)
        {
            return;
        }

        var parent = SelectedCriterion;
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
        SelectedCriterion = child;
        await ExecuteSave();
    }

    async Task DoDeleteCriterion()
    {
        if (SelectedCourse == null || SelectedCriterion == null)
        {
            return;
        }

        var criterion = SelectedCriterion;
        if (criterion.Children.Any())
        {
            return;
        }

        var hasAssessments = SelectedCourse.Classes
            .Where(c => string.IsNullOrWhiteSpace(criterion.ClassId) || c.Id == criterion.ClassId)
            .SelectMany(c => c.Assessments)
            .Any(a => a.CriterionId == criterion.Id && (!criterion.Term.HasValue || a.Term == criterion.Term));
        if (hasAssessments)
        {
            return;
        }

        if (criterion.Parent != null)
        {
            criterion.Parent.RemoveChild(criterion);
        }
        else
        {
            SelectedCourse.RemoveCriterion(criterion);
        }
        SelectedCriterion = null;
        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    public void Dispose()
    {
        courseAnchors?.Dispose();
        anchors.Dispose();
    }
}
