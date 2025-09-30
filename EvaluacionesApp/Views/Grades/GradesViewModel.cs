using System;
using System.Collections.Generic;
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

namespace EvaluacionesApp.Views.Grades;

public partial class GradesViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<DynamicCriterion> EmptyCriteria = new(new ObservableCollection<DynamicCriterion>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private CompositeDisposable? courseAnchors;
    private CompositeDisposable? classAnchors;
    private IDisposable? leafSubscription;
    private DynamicRoot? root;
    private readonly List<IDisposable> rowSubscriptions = new();

    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;
    public ReadOnlyObservableCollection<DynamicCourse> Courses
    {
        get => courses;
        private set => this.RaiseAndSetIfChanged(ref courses, value);
    }

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;

    private ReadOnlyObservableCollection<DynamicCriterion> leafCriteria = EmptyCriteria;
    public ReadOnlyObservableCollection<DynamicCriterion> LeafCriteria
    {
        get => leafCriteria;
        private set => this.RaiseAndSetIfChanged(ref leafCriteria, value);
    }

    public ObservableCollection<ScoreRow> ScoreRows { get; } = new();
    [Reactive] private ScoreRow? selectedScoreRow;

    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });
    [Reactive] private int selectedTerm = 1;

    [Reactive] private bool isDirty;

    public ReactiveCommand<Unit, Unit> Reload { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    readonly System.Timers.Timer autoSaveTimer;

    private Dictionary<string, double> weights = new();

    public GradesViewModel(DynamicSchoolStore store)
    {
        this.store = store;

        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        Reload = ReactiveCommand.CreateFromTask(DoReload);

        autoSaveTimer = new System.Timers.Timer(5000);
        autoSaveTimer.Elapsed += async (_, _) =>
        {
            if (IsDirty)
            {
                await ExecuteSave();
                IsDirty = false;
            }
        };
        autoSaveTimer.Start();

        _ = Load();

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(HandleSelectedClassChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedTerm)
            .Subscribe(_ => BuildScoreRows())
            .DisposeWith(anchors);
    }

    async Task Load()
    {
        root = await store.GetRoot();
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;
        leafSubscription?.Dispose();
        leafSubscription = null;
        LeafCriteria = EmptyCriteria;
        classAnchors?.Dispose();
        classAnchors = null;

        if (course == null)
        {
            SelectedClass = null;
            BuildScoreRows();
            return;
        }

        courseAnchors = new CompositeDisposable();

        var leafStream = course.LeafCriteria()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Publish()
            .RefCount();

        leafSubscription = leafStream
            .Bind(out ReadOnlyObservableCollection<DynamicCriterion> leaves)
            .Subscribe();
        LeafCriteria = leaves;
        courseAnchors.Add(leafSubscription);

        leafStream
            .Subscribe(_ =>
            {
                RecomputeWeights();
                BuildScoreRows();
            })
            .DisposeWith(courseAnchors);

        course.CriteriaChanges
            .AutoRefresh(c => c.Weight)
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(courseAnchors);

        SelectedClass = course.Classes.FirstOrDefault();
    }

    void HandleSelectedClassChanged(DynamicClass? cls)
    {
        classAnchors?.Dispose();
        classAnchors = null;

        if (cls == null)
        {
            BuildScoreRows();
            return;
        }

        classAnchors = new CompositeDisposable();

        cls.StudentsChanges
            .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
            .Subscribe(_ => BuildScoreRows())
            .DisposeWith(classAnchors);

        cls.AssessmentsChanges
            .AutoRefresh(a => a.Score)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(classAnchors);

        BuildScoreRows();
    }

    void BuildScoreRows()
    {
        foreach (var subscription in rowSubscriptions)
        {
            subscription.Dispose();
        }
        rowSubscriptions.Clear();

        foreach (var row in ScoreRows.ToList())
        {
            row.Dispose();
        }
        ScoreRows.Clear();

        if (SelectedClass == null || SelectedCourse == null)
        {
            return;
        }

        var leaves = LeafCriteria.ToList();
        RecomputeWeights();

        foreach (var student in SelectedClass.Students)
        {
            var row = new ScoreRow(SelectedClass, student, leaves, SelectedTerm);
            row.SetWeights(weights);
            var disp = row.Changed.Subscribe(_ => IsDirty = true);
            rowSubscriptions.Add(disp);
            ScoreRows.Add(row);
        }

        SelectedScoreRow = ScoreRows.FirstOrDefault();

    }

    void RecomputeWeights()
    {
        if (LeafCriteria.Count == 0)
        {
            weights = new();
            return;
        }

        var total = LeafCriteria.Sum(l => l.Weight);
        if (total <= 0)
        {
            total = 1;
        }

        weights = LeafCriteria.ToDictionary(l => l.Id, l => l.Weight / total);
        foreach (var row in ScoreRows)
        {
            row.SetWeights(weights);
        }
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
        IsDirty = false;
    }

    async Task DoReload()
    {
        var courseId = SelectedCourse?.Id;
        var classId = SelectedClass?.Id;
        await store.ReloadAsync();
        root = await store.GetRoot();
        Courses = root.Courses;
        SelectedCourse = courseId != null ? Courses.FirstOrDefault(c => c.Id == courseId) : Courses.FirstOrDefault();
        SelectedClass = (SelectedCourse != null && classId != null)
            ? SelectedCourse.Classes.FirstOrDefault(cl => cl.Id == classId)
            : SelectedCourse?.Classes.FirstOrDefault();
        BuildScoreRows();
    }

    public void Dispose()
    {
        foreach (var subscription in rowSubscriptions)
        {
            subscription.Dispose();
        }
        rowSubscriptions.Clear();

        foreach (var row in ScoreRows)
        {
            row.Dispose();
        }
        ScoreRows.Clear();

        classAnchors?.Dispose();
        courseAnchors?.Dispose();
        leafSubscription?.Dispose();
        anchors.Dispose();
        autoSaveTimer.Dispose();
    }
}

public class ScoreRow : ReactiveObject, IDisposable
{
    private readonly DynamicClass cls;
    private readonly Dictionary<string, DynamicAssessment> assessments = new();
    private readonly Dictionary<string, ScoreBinding> bindings = new();
    private readonly CompositeDisposable subscriptions = new();
    private Dictionary<string, double> weights = new();
    private double? cachedTotal;

    public ScoreRow(DynamicClass cls, DynamicStudent student, IEnumerable<DynamicCriterion> criteria, int term)
    {
        this.cls = cls;
        Student = student;
        Term = term;

        foreach (var criterion in criteria)
        {
            var assessment = cls.GetOrCreateAssessment(student.Id, criterion.Id, term);
            assessments[criterion.Id] = assessment;
            RegisterAssessment(criterion.Id, assessment);
        }
    }

    public DynamicStudent Student { get; }

    public int Term { get; }

    public ScoreBinding this[string criterionId]
    {
        get
        {
            if (!bindings.TryGetValue(criterionId, out var binding))
            {
                binding = new ScoreBinding(this, criterionId);
                bindings[criterionId] = binding;
            }
            return binding;
        }
    }

    internal double? GetScore(string criterionId)
    {
        return assessments.TryGetValue(criterionId, out var assessment) ? assessment.Score : null;
    }

    internal void SetScore(string criterionId, double? value)
    {
        if (assessments.TryGetValue(criterionId, out var assessment))
        {
            assessment.Score = value;
        }
    }

    void RegisterAssessment(string criterionId, DynamicAssessment assessment)
    {
        var disp = assessment.WhenAnyValue(a => a.Score)
            .Subscribe(_ =>
            {
                cachedTotal = null;
                this.RaisePropertyChanged(nameof(Total));
                if (bindings.TryGetValue(criterionId, out var binding))
                {
                    binding.NotifyChanged();
                }
            });
        subscriptions.Add(disp);
    }

    public double Total
    {
        get
        {
            cachedTotal ??= assessments.Sum(kv => (kv.Value.Score ?? 0) * weights.GetValueOrDefault(kv.Key));
            return cachedTotal.Value;
        }
    }

    public void SetWeights(Dictionary<string, double> map)
    {
        weights = map;
        cachedTotal = null;
        this.RaisePropertyChanged(nameof(Total));
    }

    public void Dispose()
    {
        subscriptions.Dispose();
    }
}

public class ScoreBinding : ReactiveObject
{
    private readonly ScoreRow row;
    private readonly string criterionId;

    public ScoreBinding(ScoreRow row, string criterionId)
    {
        this.row = row;
        this.criterionId = criterionId;
    }

    public double? Value
    {
        get => row.GetScore(criterionId);
        set => row.SetScore(criterionId, value);
    }

    internal void NotifyChanged()
    {
        this.RaisePropertyChanged(nameof(Value));
    }
}
