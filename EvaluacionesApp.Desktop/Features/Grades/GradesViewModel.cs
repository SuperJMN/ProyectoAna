using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
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
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.Grades;

[Section(name: "Grades", icon: "mdi-numeric-3-box-multiple", sortIndex: 0, FriendlyName = "Notas")]

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
    private Dictionary<string, decimal> weights = new();
    private DynamicRoot? root;

    [Reactive]
    private IEnumerable<ScopedCriterionNode> criteriaTree = Enumerable.Empty<ScopedCriterionNode>();

    private readonly Zafiro.UI.INotificationService? notifications;

    public GradesViewModel(IDynamicSchoolStore store, Zafiro.UI.INotificationService notifications, IScheduler? scheduler = null, TimeSpan? autoSaveInterval = null)
        : this(store, scheduler, autoSaveInterval)
    {
        this.notifications = notifications;
        Save.ThrownExceptions
            .Subscribe(ex => _ = notifications.Show("No se pudieron guardar las notas", ex.Message))
            .DisposeWith(anchors);
    }

    public GradesViewModel(IDynamicSchoolStore store, IScheduler? scheduler = null, TimeSpan? autoSaveInterval = null)
    {
        this.store = store;
        this.scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;
        var interval = autoSaveInterval ?? TimeSpan.FromSeconds(5);

        LeafCriteria = new ReadOnlyObservableCollection<DynamicCriterion>(leafCriteriaInternal);

        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        Reload = ReactiveCommand.CreateFromTask(DoReload);
        RebuildRows = ReactiveCommand.Create(RebuildScoreRows);
        OpenScoreDetails = ReactiveCommand.Create<ScoreRow?>(OpenDetailsForRow);
        ShowScoreRows = ReactiveCommand.Create(HideScoreDetails);

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
            .Replay(1)
            .RefCount();

        selectedCourseChanges
            .Select(course => course?.Classes.FirstOrDefault())
            .ObserveOn(this.scheduler)
            .BindTo(this, x => x.SelectedClass)
            .DisposeWith(anchors);

        var weightChanges = selectedCourseChanges
            .Select(course => course != null
                ? course.AllCriteria()
                    .AutoRefresh(c => c.Weight)
                    .Where(changes => changes.Any(change => change.Reason == ChangeReason.Refresh))
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

        this.WhenAnyValue(x => x.SelectedClass, x => x.SelectedTerm)
            .Subscribe(_ => HideScoreDetails())
            .DisposeWith(anchors);

        selectedCourseChanges
            .Select(course => course != null
                ? course.TermsChanges
                    .Select(_ => Unit.Default)
                    .StartWith(Unit.Default)
                : Observable.Return(Unit.Default))
            .Switch()
            .ObserveOn(this.scheduler)
            .Subscribe(_ => UpdateTerms(SelectedCourse))
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
    [Reactive] private bool areScoreDetailsShown;

    public ObservableCollection<int> Terms { get; } = new();
    [Reactive] private int selectedTerm = 1;


    public ReactiveCommand<Unit, Unit> Reload { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> RebuildRows { get; }
    public ReactiveCommand<ScoreRow?, Unit> OpenScoreDetails { get; }
    public ReactiveCommand<Unit, Unit> ShowScoreRows { get; }

    async Task Load()
    {
        root = store.Root;
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
        UpdateTerms(SelectedCourse);
        await Task.CompletedTask;
    }

    void RebuildScoreRows()
    {
        var previousSelection = Maybe<ScoreRow>.From(SelectedScoreRow).Map(row => row.Student.Id);

        scoreRowsCache.Edit(cache => cache.Clear());
        leafCriteriaInternal.Clear();
        weights = new Dictionary<string, decimal>();

        var courseOption = Maybe<DynamicCourse>.From(SelectedCourse);
        var classOption = Maybe<DynamicClass>.From(SelectedClass);

        if (courseOption.HasNoValue || classOption.HasNoValue)
        {
            SelectedScoreRow = null;
            CriteriaTree = Enumerable.Empty<ScopedCriterionNode>();
            return;
        }

        var course = courseOption.Value;
        var cls = classOption.Value;
        var tree = course.FilterCriteriaTree(SelectedTerm)
            .Select(root => ScopedCriterionNode.Build(root, SelectedTerm))
            .Where(node => node != null)
            .Select(node => node!)
            .ToList();
        CriteriaTree = tree;
        var leaves = course.EnumerateLeafCriteria(SelectedTerm).ToList();

        foreach (var leaf in leaves)
        {
            leafCriteriaInternal.Add(leaf);
        }

        weights = ComputeWeights(tree);

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

    static Dictionary<string, decimal> ComputeWeights(IReadOnlyCollection<ScopedCriterionNode> roots)
    {
        if (roots.Count == 0)
        {
            return new Dictionary<string, decimal>();
        }

        var contributions = new Dictionary<string, decimal>();
        var rootEntries = roots
            .Select(node => (Node: node, Weight: Math.Max(node.Criterion.Weight, 0m)))
            .ToList();

        if (rootEntries.Count == 0)
        {
            return contributions;
        }

        var rootWeightSum = rootEntries.Sum(entry => entry.Weight);
        if (rootWeightSum <= 0m)
        {
            var equalShare = 1m / rootEntries.Count;
            foreach (var entry in rootEntries)
            {
                DistributeWeight(entry.Node, equalShare, contributions);
            }
            return contributions;
        }

        foreach (var entry in rootEntries)
        {
            var share = entry.Weight / rootWeightSum;
            DistributeWeight(entry.Node, share, contributions);
        }

        return contributions;
    }

    static void DistributeWeight(
        ScopedCriterionNode node,
        decimal allocatedWeight,
        IDictionary<string, decimal> contributions)
    {
        if (node.Children.Count == 0)
        {
            contributions[node.Id] = allocatedWeight;
            return;
        }

        var childEntries = node.Children
            .Select(child => (Node: child, Weight: Math.Max(child.Criterion.Weight, 0m)))
            .ToList();

        if (childEntries.Count == 0)
        {
            contributions[node.Id] = allocatedWeight;
            return;
        }

        var weightSum = childEntries.Sum(entry => entry.Weight);
        if (weightSum <= 0m)
        {
            var equalShare = allocatedWeight / childEntries.Count;
            foreach (var entry in childEntries)
            {
                DistributeWeight(entry.Node, equalShare, contributions);
            }
            return;
        }

        foreach (var entry in childEntries)
        {
            var share = allocatedWeight * (entry.Weight / weightSum);
            DistributeWeight(entry.Node, share, contributions);
        }
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    async Task DoReload()
    {
        // Note: Reload functionality would need to be implemented at application level
        // For now, just rebuild the current state
        RebuildScoreRows();
        await Task.CompletedTask;
    }

    void OpenDetailsForRow(ScoreRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedScoreRow = row;
        AreScoreDetailsShown = true;
    }

    void HideScoreDetails()
    {
        AreScoreDetailsShown = false;
    }

    void UpdateTerms(DynamicCourse? course)
    {
        Terms.Clear();

        IEnumerable<int> source = course != null && course.Terms.Count > 0
            ? course.Terms
            : new[] { 1, 2, 3 };

        foreach (var term in source.Distinct().OrderBy(x => x))
        {
            Terms.Add(term);
        }

        if (Terms.Count == 0)
        {
            Terms.Add(1);
            Terms.Add(2);
            Terms.Add(3);
        }

        if (!Terms.Contains(SelectedTerm))
        {
            SelectedTerm = Terms.First();
        }
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
    private Dictionary<string, decimal> weights = new();

    public ScoreRow(DynamicClass cls, DynamicStudent student, IEnumerable<DynamicCriterion> criteria, int term, IScheduler scheduler)
    {
        Student = student;
        Term = term;
        this.scheduler = scheduler;
        @class = cls;

        foreach (var criterion in criteria)
        {
            var assessment = cls.GetOrCreateAssessment(student.Id, criterion.Id);
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

    internal decimal? GetScore(string criterionId)
    {
        var optional = assessments.Lookup(criterionId);
        if (optional.HasValue)
        {
            return optional.Value.Score;
        }

        var existing = @class.Assessments.FirstOrDefault(assessment =>
            string.Equals(assessment.StudentId, Student.Id, StringComparison.Ordinal) &&
            string.Equals(assessment.CriterionId, criterionId, StringComparison.Ordinal));

        if (existing != null)
        {
            assessments.AddOrUpdate(existing);
            return existing.Score;
        }

        return null;
    }

    internal void SetScore(string criterionId, decimal? value)
    {
        var assessment = EnsureAssessment(criterionId);
        assessment.Score = value;
    }

    decimal CalculateTotal()
    {
        return assessments.Items.Sum(assessment => (assessment.Score ?? 0m) * weights.GetValueOrDefault(assessment.CriterionId, 0m));
    }

    [ObservableAsProperty(PropertyName = nameof(Total))]
    private IObservable<decimal> TotalObservable()
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

    public void SetWeights(Dictionary<string, decimal> map)
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

        var assessment = @class.GetOrCreateAssessment(Student.Id, criterionId);
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
    private readonly ObservableAsPropertyHelper<decimal?> value;

    public ScoreBinding(DynamicAssessment assessment, IScheduler scheduler)
    {
        this.assessment = assessment;
        value = assessment
            .WhenAnyValue(a => a.Score)
            .StartWith(assessment.Score)
            .ObserveOn(scheduler)
            .ToProperty(this, binding => binding.Value);
    }

    public decimal? Value
    {
        get => value.Value;
        set => assessment.Score = value;
    }
}
