using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Grades;

public partial class GradesViewModel : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();

    [Reactive] private Course? selectedCourse;
    [Reactive] private Class? selectedClass;

    private ObservableCollection<ScoreRow> scoreRows = new();
    public ObservableCollection<ScoreRow> ScoreRows 
    { 
        get => scoreRows; 
        private set => this.RaiseAndSetIfChanged(ref scoreRows, value); 
    }
    
    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });
    [Reactive] private int selectedTerm = 1;

    readonly PersistenceService persistence;
    readonly System.Timers.Timer autoSaveTimer;
    [Reactive] private bool isDirty;
    Dictionary<string, double> weights = new();

    public GradesViewModel(PersistenceService persistence)
    {
        this.persistence = persistence;

        autoSaveTimer = new System.Timers.Timer(5000); // Changed from 1s to 5s
        autoSaveTimer.Elapsed += async (_, _) =>
        {
            if (IsDirty)
            {
                await Save();
                IsDirty = false;
            }
        };
        autoSaveTimer.Start();

        Load();
        
        // Subscribe to changes after initial load
        var trigger = System.Reactive.Linq.Observable.Merge(
            this.WhenAnyValue(x => x.SelectedCourse).Select(_ => System.Reactive.Unit.Default),
            this.WhenAnyValue(x => x.SelectedClass).Select(_ => System.Reactive.Unit.Default),
            this.WhenAnyValue(x => x.SelectedTerm).Select(_ => System.Reactive.Unit.Default));
        trigger.Subscribe(System.Reactive.Observer.Create<System.Reactive.Unit>(_ => BuildScoreRows()));
    }

    async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses)
            Courses.Add(c);

        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
    }

    public async System.Threading.Tasks.Task Reload()
    {
        var courseId = SelectedCourse?.Id;
        var classId = SelectedClass?.Id;
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses)
            Courses.Add(c);
        SelectedCourse = courseId != null ? Courses.FirstOrDefault(c => c.Id == courseId) : Courses.FirstOrDefault();
        SelectedClass = (SelectedCourse != null && classId != null)
            ? SelectedCourse.Classes.FirstOrDefault(cl => cl.Id == classId)
            : SelectedCourse?.Classes.FirstOrDefault();
        BuildScoreRows();
    }

    void BuildScoreRows()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        if (SelectedCourse == null || SelectedClass == null)
        {
            if (ScoreRows.Count > 0)
                ScoreRows = new ObservableCollection<ScoreRow>();
            return;
        }
        
        // Defer clearing until we have new data ready
        Console.WriteLine($"[PERF] Start BuildScoreRows: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Use data already in memory instead of reloading from disk
        var leaves = GetLeafCriteria(SelectedCourse.Criteria).ToList();
        Console.WriteLine($"[PERF] Get leaf criteria: {sw.ElapsedMilliseconds}ms, Count: {leaves.Count}");
        sw.Restart();
        
        RecomputeWeights();
        Console.WriteLine($"[PERF] Recompute weights: {sw.ElapsedMilliseconds}ms");
        sw.Restart();
        
        // Group by key to handle duplicates, taking the last value
        var existing = (SelectedClass.Assessments ?? new List<Assessment>())
            .Where(a => a.Term.GetValueOrDefault(SelectedTerm) == SelectedTerm && a.StudentId != null && a.CriterionId != null)
            .GroupBy(a => (a.StudentId!, a.CriterionId!))
            .ToDictionary(g => g.Key, g => g.Last().Score);
        Console.WriteLine($"[PERF] Build existing dict: {sw.ElapsedMilliseconds}ms, Count: {existing.Count}");
        sw.Restart();

        int studentCount = SelectedClass.Students.Count;
        var newRows = new List<ScoreRow>(studentCount);
        
        foreach (var student in SelectedClass.Students)
        {
            var row = new ScoreRow(student);
            row.SetWeights(weights);
            foreach (var leaf in leaves)
            {
                var key = (student.Id, leaf.Id);
                row.Scores[leaf.Id] = existing.TryGetValue(key, out var v) ? v : null;
            }
            newRows.Add(row);
        }
        
        Console.WriteLine($"[PERF] Create {studentCount} rows in memory: {sw.ElapsedMilliseconds}ms");
        sw.Restart();
        
        // Replace entire collection to avoid individual add notifications
        ScoreRows = new ObservableCollection<ScoreRow>(newRows);
        
        Console.WriteLine($"[PERF] Replace entire collection: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"[PERF] Total BuildScoreRows: {sw.Elapsed.TotalMilliseconds}ms");
    }

    public static IEnumerable<Criterion> GetLeafCriteria(IEnumerable<Criterion> nodes)
    {
        foreach (var n in nodes)
        {
            if (n.Children.Count == 0) yield return n;
            else foreach (var c in GetLeafCriteria(n.Children)) yield return c;
        }
    }

    public void RecomputeWeights()
    {
        if (SelectedCourse == null)
        {
            weights = new();
            return;
        }
        var leaves = GetLeafCriteria(SelectedCourse.Criteria).ToList();
        var total = leaves.Sum(l => l.Weight);
        if (total <= 0) total = 1;
        weights = leaves.ToDictionary(l => l.Id, l => (l.Weight / total));
        foreach (var row in ScoreRows)
        {
            row.SetWeights(weights);
            row.Touch();
        }
    }

    public void NotifyScoreEdited()
    {
        IsDirty = true;
    }

    async System.Threading.Tasks.Task Save()
    {
        if (SelectedCourse == null || SelectedClass == null) return;
        var leaves = GetLeafCriteria(SelectedCourse.Criteria).ToList();
        // Remove existing for this term
        var remaining = SelectedClass.Assessments.Where(a => a.Term.GetValueOrDefault(SelectedTerm) != SelectedTerm).ToList();
        var newOnes = new System.Collections.Generic.List<Assessment>();
        foreach (var row in ScoreRows)
        {
            foreach (var leaf in leaves)
            {
                if (row.Scores.TryGetValue(leaf.Id, out var v) && v.HasValue)
                {
                    newOnes.Add(new Assessment { StudentId = row.Student.Id, CriterionId = leaf.Id, Term = SelectedTerm, Score = v.Value });
                }
            }
        }
        SelectedClass.Assessments = remaining.Concat(newOnes).ToList();
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}

public class ScoreRow : ReactiveObject
{
    public Student Student { get; }
    private readonly Dictionary<string, double?> scores = new();
    private Dictionary<string, double> weights = new();
    private double? cachedTotal;
    
    public Dictionary<string, double?> Scores => scores;
    
    public double Total
    {
        get
        {
            if (!cachedTotal.HasValue)
            {
                cachedTotal = scores.Sum(kv => (kv.Value ?? 0) * (weights.TryGetValue(kv.Key, out var w) ? w : 0));
            }
            return cachedTotal.Value;
        }
    }

    public void SetWeights(Dictionary<string, double> map)
    {
        weights = map;
        cachedTotal = null;
    }
    
    public void Touch()
    {
        cachedTotal = null;
        this.RaisePropertyChanged(nameof(Total));
    }
    
    public void SetScore(string criterionId, double? value)
    {
        scores[criterionId] = value;
        Touch();
    }

    public ScoreRow(Student student) { Student = student; }
}
