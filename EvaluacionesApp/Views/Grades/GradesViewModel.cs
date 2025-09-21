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

    public ObservableCollection<ScoreRow> ScoreRows { get; } = new();
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
        System.Diagnostics.Debug.WriteLine($"[GradesVM.Reload] Course: {SelectedCourse?.Name}, Class: {SelectedClass?.Name}, Students: {SelectedClass?.Students.Count}");
    }

    void BuildScoreRows()
    {
        ScoreRows.Clear();
        if (SelectedCourse == null || SelectedClass == null)
        {
            System.Diagnostics.Debug.WriteLine($"[BuildScoreRows] Early return: Course={SelectedCourse?.Name}, Class={SelectedClass?.Name}");
            return;
        }

        // Use data already in memory instead of reloading from disk
        var leaves = GetLeafCriteria(SelectedCourse.Criteria).ToList();
        RecomputeWeights();
        
        // Group by key to handle duplicates, taking the last value
        var existing = (SelectedClass.Assessments ?? new List<Assessment>())
            .Where(a => a.Term.GetValueOrDefault(SelectedTerm) == SelectedTerm && a.StudentId != null && a.CriterionId != null)
            .GroupBy(a => (a.StudentId!, a.CriterionId!))
            .ToDictionary(g => g.Key, g => g.Last().Score);

        foreach (var student in SelectedClass.Students)
        {
            var row = new ScoreRow(student);
            row.SetWeights(weights);
            foreach (var leaf in leaves)
            {
                var key = (student.Id, leaf.Id);
                row.Scores[leaf.Id] = existing.TryGetValue(key, out var v) ? v : null;
            }
            ScoreRows.Add(row);
        }
        
        System.Diagnostics.Debug.WriteLine($"[BuildScoreRows] Created {ScoreRows.Count} rows");
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
    public Dictionary<string, double?> Scores { get; } = new();
    Dictionary<string, double> weights = new();
    public double Total => Scores.Sum(kv => (kv.Value ?? 0) * (weights.TryGetValue(kv.Key, out var w) ? w : 0));

    public void SetWeights(Dictionary<string, double> map) => weights = map;
    public void Touch() => this.RaisePropertyChanged(nameof(Total));

    public ScoreRow(Student student) { Student = student; }
}
