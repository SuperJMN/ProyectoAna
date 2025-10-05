using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Views.Grades;

public partial class GradesViewModel : ReactiveObject, IDisposable
{
    private readonly IDynamicSchoolStore store;
    private readonly IScheduler scheduler;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<ScoreRow, string> scoreRowsCache = new(row => row.Student.Id);
    private readonly ObservableCollection<DynamicCriterion> leafCriteriaInternal = new();
    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = new(new ObservableCollection<DynamicCourse>());

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<ScoreRow> scoreRows = new(new ObservableCollection<ScoreRow>());
    private Dictionary<string, double> weights = new();
    private DynamicRoot? root;

    [Reactive]
    private IEnumerable<ScopedCriterionNode> criteriaTree = Enumerable.Empty<ScopedCriterionNode>();

    public GradesViewModel(IDynamicSchoolStore store, IScheduler? scheduler = null, TimeSpan? autoSaveInterval = null)
    {
        this.store = store;
        this.scheduler = scheduler ?? RxApp.MainThreadScheduler;
        var interval = autoSaveInterval ?? TimeSpan.FromSeconds(5);

        LeafCriteria = new ReadOnlyObservableCollection<DynamicCriterion>(leafCriteriaInternal);

        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        Reload = ReactiveCommand.CreateFromTask(DoReload);
        RebuildRows = ReactiveCommand.Create(RebuildScoreRows);

        scoreRowsCache
            .Connect()
            .ObserveOn(this.scheduler)
            .Bind(out scoreRows)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(anchors);

        ScoreRows = scoreRows;

        Initialization = Load();

        var selectedCourseChanges = this.WhenAnyValue(x => x.SelectedCourse)
            .Publish()
            .RefCount();

        selectedCourseChanges
            .Select(course => course?.Classes.FirstOrDefault())
            .ObserveOn(this.scheduler)
            .BindTo(this, x => x.SelectedClass)
            .DisposeWith(anchors);

        var weightChanges = selectedCourseChanges
            .Select(course => course != null
                ? course.CriteriaChanges
                    .AutoRefresh(c => c.Weight)
                    .Throttle(TimeSpan.FromMilliseconds(400), this.scheduler)
                    .Select(_ => Unit.Default)
                : Observable.Empty<Unit>())
            .Switch()
            .Publish()
            .RefCount();

        weightChanges
            .InvokeCommand(RebuildRows)
            .DisposeWith(anchors);

        var leafCriteriaChanges = selectedCourseChanges
            .Select(course => course != null
                ? course.LeafCriteria().Select(_ => Unit.Default).StartWith(Unit.Default)
                : Observable.Return(Unit.Default))
            .Switch();

        leafCriteriaChanges
            .Merge(this.WhenAnyValue(x => x.SelectedClass).Select(_ => Unit.Default))
            .Merge(this.WhenAnyValue(x => x.SelectedTerm).Select(_ => Unit.Default))
            .InvokeCommand(RebuildRows)
            .DisposeWith(anchors);

        var studentChanges = this.WhenAnyValue(x => x.SelectedClass)
            .Select(cls => cls != null
                ? cls.StudentsChanges.Throttle(TimeSpan.FromMilliseconds(200), this.scheduler).Select(_ => Unit.Default)
                : Observable.Return(Unit.Default))
            .Switch()
            .Publish()
            .RefCount();

        studentChanges
            .InvokeCommand(RebuildRows)
            .DisposeWith(anchors);

        var assessmentChanges = this.WhenAnyValue(x => x.SelectedClass)
            .Select(cls => cls != null
                ? cls.AssessmentsChanges
                    .AutoRefresh(a => a.Score)
                    .Select(_ => Unit.Default)
                : Observable.Empty<Unit>())
            .Switch()
            .Publish()
            .RefCount();

        var saveSignals = Observable.Merge(weightChanges, assessmentChanges);

        var savePipeline = interval > TimeSpan.Zero
            ? saveSignals.Throttle(interval, this.scheduler)
            : saveSignals;

        savePipeline
            .InvokeCommand(Save)
            .DisposeWith(anchors);
    }

    public Task Initialization { get; }

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
    public ReadOnlyObservableCollection<DynamicCriterion> LeafCriteria { get; }

    [Reactive] private ScoreRow? selectedScoreRow;

    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });
    [Reactive] private int selectedTerm = 1;


    public ReactiveCommand<Unit, Unit> Reload { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> RebuildRows { get; }

    async Task Load()
    {
        root = await store.GetRoot();
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
    }

    void RebuildScoreRows()
    {
        var previousSelection = Maybe<ScoreRow>.From(SelectedScoreRow).Map(row => row.Student.Id);

        scoreRowsCache.Edit(cache => cache.Clear());
        leafCriteriaInternal.Clear();
        weights = new Dictionary<string, double>();

        var selection = Maybe<DynamicCourse>.From(SelectedCourse)
            .Bind(course => Maybe<DynamicClass>.From(SelectedClass).Map(cls => (course, cls)));

        if (selection.HasNoValue)
        {
            SelectedScoreRow = null;
            CriteriaTree = Enumerable.Empty<ScopedCriterionNode>();
            return;
        }

        var (course, cls) = selection.Value;
        CriteriaTree = course.FilterCriteriaTree(cls.Id, SelectedTerm)
            .Select(root => ScopedCriterionNode.Build(root, cls.Id, SelectedTerm))
            .Where(node => node != null)
            .Select(node => node!)
            .ToList();
        var leaves = course.EnumerateLeafCriteria(cls.Id, SelectedTerm).ToList();

        foreach (var leaf in leaves)
        {
            leafCriteriaInternal.Add(leaf);
        }

        weights = ComputeWeights(leaves);

        scoreRowsCache.Edit(cache =>
        {
            foreach (var student in cls.Students)
            {
                var row = new ScoreRow(cls, student, leaves, SelectedTerm, scheduler);
                row.SetWeights(weights);
                cache.AddOrUpdate(row);
            }
        });

        SelectedScoreRow = previousSelection
            .Bind(id => Maybe<ScoreRow>.From(ScoreRows.FirstOrDefault(r => r.Student.Id == id)))
            .Match(value => value, () => ScoreRows.FirstOrDefault());
    }

    static Dictionary<string, double> ComputeWeights(IReadOnlyCollection<DynamicCriterion> leaves)
    {
        if (leaves.Count == 0)
        {
            return new Dictionary<string, double>();
        }

        var total = leaves.Sum(l => l.Weight);
        if (total <= 0)
        {
            total = 1;
        }

        return leaves.ToDictionary(leaf => leaf.Id, leaf => leaf.Weight / total);
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    async Task DoReload()
    {
        var previousCourse = Maybe<DynamicCourse>.From(SelectedCourse).Map(c => c.Id);
        var previousClass = Maybe<DynamicClass>.From(SelectedClass).Map(c => c.Id);

        await store.ReloadAsync();
        root = await store.GetRoot();
        Courses = root.Courses;

        SelectedCourse = previousCourse
            .Bind(id => Maybe<DynamicCourse>.From(Courses.FirstOrDefault(c => c.Id == id)))
            .Match(value => value, () => Courses.FirstOrDefault());

        SelectedClass = previousClass
            .Bind(id => Maybe<DynamicClass>.From(SelectedCourse?.Classes.FirstOrDefault(cls => cls.Id == id)))
            .Match(value => value, () => SelectedCourse?.Classes.FirstOrDefault());

        RebuildScoreRows();
    }

    public void Dispose()
    {
        anchors.Dispose();
        scoreRowsCache.Dispose();
        foreach (var row in ScoreRows)
        {
            row.Dispose();
        }
    }
}

public partial class ScoreRow : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicAssessment, string> assessments = new(assessment => assessment.CriterionId);
    private readonly Dictionary<string, ScoreBinding> bindingCache = new();
    private readonly CompositeDisposable anchors = new();
    private readonly Subject<Unit> weightChanges = new();
    private readonly IScheduler scheduler;
    private readonly DynamicClass @class;
    private Dictionary<string, double> weights = new();

    public ScoreRow(DynamicClass cls, DynamicStudent student, IEnumerable<DynamicCriterion> criteria, int term, IScheduler scheduler)
    {
        Student = student;
        Term = term;
        this.scheduler = scheduler;
        @class = cls;

        foreach (var criterion in criteria)
        {
            var assessment = cls.GetOrCreateAssessment(student.Id, criterion.Id, term);
            assessments.AddOrUpdate(assessment);
        }

        InitializeOAPH();
    }

    public DynamicStudent Student { get; }

    public int Term { get; }

    public ScoreBinding this[string criterionId]
    {
        get
        {
            if (bindingCache.TryGetValue(criterionId, out var binding))
            {
                return binding;
            }

            var assessment = EnsureAssessment(criterionId);
            var created = new ScoreBinding(assessment, scheduler);
            bindingCache[criterionId] = created;
            return created;
        }
    }

    internal double? GetScore(string criterionId)
    {
        var optional = assessments.Lookup(criterionId);
        return optional.HasValue ? optional.Value.Score : null;
    }

    internal void SetScore(string criterionId, double? value)
    {
        var assessment = EnsureAssessment(criterionId);
        assessment.Score = value;
    }

    double CalculateTotal()
    {
        return assessments.Items.Sum(assessment => (assessment.Score ?? 0) * weights.GetValueOrDefault(assessment.CriterionId));
    }

    [ObservableAsProperty(PropertyName = nameof(Total))]
    private IObservable<double> TotalObservable()
    {
        return Observable.Merge(
                assessments.Connect()
                    .AutoRefresh(a => a.Score)
                    .Select(_ => Unit.Default),
                weightChanges)
            .Select(_ => CalculateTotal())
            .StartWith(CalculateTotal())
            .ObserveOn(scheduler);
    }

    public void SetWeights(Dictionary<string, double> map)
    {
        weights = map;
        weightChanges.OnNext(Unit.Default);
    }

    DynamicAssessment EnsureAssessment(string criterionId)
    {
        var optional = assessments.Lookup(criterionId);
        if (optional.HasValue)
        {
            return optional.Value;
        }

        var assessment = @class.GetOrCreateAssessment(Student.Id, criterionId, Term);
        assessments.AddOrUpdate(assessment);
        return assessment;
    }

    public void Dispose()
    {
        anchors.Dispose();
        assessments.Dispose();
        weightChanges.Dispose();
    }
}

public class ScoreBinding : ReactiveObject
{
    private readonly DynamicAssessment assessment;
    private readonly ObservableAsPropertyHelper<double?> value;

    public ScoreBinding(DynamicAssessment assessment, IScheduler scheduler)
    {
        this.assessment = assessment;
        value = assessment
            .WhenAnyValue(a => a.Score)
            .StartWith(assessment.Score)
            .ObserveOn(scheduler)
            .ToProperty(this, binding => binding.Value);
    }

    public double? Value
    {
        get => value.Value;
        set => assessment.Score = value;
    }
}
