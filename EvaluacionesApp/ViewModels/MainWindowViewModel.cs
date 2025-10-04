using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<CourseVm> Courses { get; } = new();

    [Reactive]
    private CourseVm? selectedCourse;

    [Reactive]
    private ClassVm? selectedClass;

    public ObservableCollection<ScoreRowVm> ScoreRows { get; } = new();

    public ObservableCollection<int> Terms { get; } = new(new[] { 1, 2, 3 });

    [Reactive]
    private int selectedTerm = 1;

    public IReadOnlyList<CriterionVm> LeafCriteria =>
        SelectedCourse == null ? Array.Empty<CriterionVm>() : GetLeafCriteria(SelectedCourse, SelectedClass, SelectedTerm);

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddCourse { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddClass { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddStudent { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddCriterionRoot { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddCriterionChild { get; }

    readonly PersistenceService persistence;

    IDisposable? saveSubscription;
    System.Timers.Timer? autoSaveTimer;

    [Reactive]
    private bool isDirty;

    public MainWindowViewModel()
    {
        persistence = new PersistenceService(Path.Combine(Directory.GetCurrentDirectory(), "persistencia.json"));
        var canCourse = this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null);
        var canStudent = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);

        AddCourse = ReactiveCommand.Create(DoAddCourse);
        AddClass = ReactiveCommand.Create(DoAddClass, canCourse);
        AddStudent = ReactiveCommand.Create(DoAddStudent, canStudent);
        AddCriterionRoot = ReactiveCommand.Create(DoAddCriterionRoot, canCourse);
        AddCriterionChild = ReactiveCommand.Create(DoAddCriterionChild, canCourse);

        _ = Initialize();
        HookSelectionSubscriptions();

        this.WhenAnyValue(x => x.SelectedTerm)
            .Subscribe(_ =>
            {
                BuildScoreRows();
                this.RaisePropertyChanged(nameof(LeafCriteria));
            });
    }

    async Task Initialize()
    {
        var root = await persistence.Load();
        foreach (var c in root.Courses)
        {
            Courses.Add(new CourseVm(c));
        }

        // Auto-select first course/class if present
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();

        BuildScoreRows();

        // Auto-save on changes with throttle
        var courseChanges = Observable.FromEventPattern<System.Collections.Specialized.NotifyCollectionChangedEventHandler, System.Collections.Specialized.NotifyCollectionChangedEventArgs>(
            h => Courses.CollectionChanged += h,
            h => Courses.CollectionChanged -= h);

        var propChanges = Observable.FromEventPattern<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
            h => PropertyChanged += h,
            h => PropertyChanged -= h)
            .Where(e => e.EventArgs.PropertyName == nameof(SelectedCourse) || e.EventArgs.PropertyName == nameof(SelectedClass));

        var saveTrigger = Observable.Merge(
                courseChanges.Select(_ => 0),
                propChanges.Select(_ => 0))
            .Throttle(TimeSpan.FromMilliseconds(500));

        saveSubscription = saveTrigger.Subscribe(async _ => { IsDirty = true; await Save(); });

        // Fallback autosave every 2 seconds if changes detected in cells
        autoSaveTimer = new System.Timers.Timer(2000);
        autoSaveTimer.Elapsed += async (_, _) =>
        {
            if (IsDirty)
            {
                await Save();
                IsDirty = false;
            }
        };
        autoSaveTimer.Start();
    }

    async Task Save()
    {
        // Push current scores back into selected class model assessments
        if (SelectedClass != null && SelectedCourse != null)
        {
            var leafCriteria = LeafCriteria.Select(c => c.Id).ToHashSet();
            var updates = new List<Assessment>();
            foreach (var row in ScoreRows)
            {
                foreach (var kv in row.Scores)
                {
                    if (!leafCriteria.Contains(kv.Key)) continue;
                    if (kv.Value is double v)
                    {
                        updates.Add(new Assessment
                        {
                            StudentId = row.Student.Id,
                            CriterionId = kv.Key,
                            Score = v,
                            Term = SelectedTerm
                        });
                    }
                }
            }
            // Update backing model for the selected class
            var model = SelectedClass.Model;
            model.Assessments = AssessmentUtilities.MergeAssessments(
                model.Assessments,
                updates,
                SelectedTerm);
        }

        var root = new Root();
        root.Courses = Courses.Select(c => c.ToModel()).ToList();
        await persistence.Save(root);
    }

    void DoAddCourse()
    {
        var idx = Courses.Count + 1;
        var model = new Course { Id = $"course-{idx}", Name = $"Course {idx}" };
        var vm = new CourseVm(model);
        Courses.Add(vm);
        SelectedCourse = vm;
    }

    void DoAddClass()
    {
        if (SelectedCourse == null) return;
        var idx = SelectedCourse.Classes.Count + 1;
        var model = new Class { Id = $"{SelectedCourse.Id}-class-{idx}", Name = $"Class {idx}" };
        var vm = new ClassVm(model);
        SelectedCourse.Classes.Add(vm);
        SelectedClass = vm;
        BuildScoreRows();
    }

    void DoAddStudent()
    {
        if (SelectedClass == null) return;
        var idx = SelectedClass.Students.Count + 1;
        var id = Guid.NewGuid().ToString();
        var model = new Student { Id = id, Name = $"Student {idx}" };
        var vm = new StudentVm(model);
        SelectedClass.Students.Add(vm);
        BuildScoreRows();
    }

    void DoAddCriterionRoot()
    {
        if (SelectedCourse == null) return;
        var idx = SelectedCourse.Criteria.Count + 1;
        var model = new Criterion
        {
            Id = $"C{idx}",
            Name = $"Criterion {idx}",
            Weight = 1,
            ClassId = SelectedClass?.Id ?? string.Empty,
            Term = SelectedTerm
        };
        SelectedCourse.Criteria.Add(new CriterionVm(model));
        BuildScoreRows();
        this.RaisePropertyChanged(nameof(LeafCriteria));
    }

    void DoAddCriterionChild()
    {
        if (SelectedCourse == null) return;
        // Add child to the last criterion for simplicity
        var parent = SelectedCourse.Criteria.LastOrDefault();
        if (parent == null) return;
        var idx = parent.Children.Count + 1;
        var model = new Criterion
        {
            Id = $"{parent.Id}.{idx}",
            Name = $"Subcriterion {idx}",
            Weight = 1,
            ClassId = parent.ClassId,
            Term = parent.Term
        };
        parent.Children.Add(new CriterionVm(model));
        BuildScoreRows();
        this.RaisePropertyChanged(nameof(LeafCriteria));
    }

    void HookSelectionSubscriptions()
    {
        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(value =>
            {
                SelectedClass = value?.Classes.FirstOrDefault();
                BuildScoreRows();
                this.RaisePropertyChanged(nameof(LeafCriteria));
            });

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(_ =>
            {
                BuildScoreRows();
            });
    }

    void BuildScoreRows()
    {
        ScoreRows.Clear();
        if (SelectedClass == null || SelectedCourse == null) return;

        var leafCriteria = GetLeafCriteria(SelectedCourse, SelectedClass, SelectedTerm);
        var existing = AssessmentUtilities.BuildScoreLookup(SelectedClass.Model.Assessments, SelectedTerm);

        foreach (var s in SelectedClass.Students)
        {
            var row = new ScoreRowVm(s);
            foreach (var cr in leafCriteria)
            {
                var key = (s.Id, cr.Id);
                row.Scores[cr.Id] = existing.TryGetValue(key, out var score)
                    ? score
                    : null;
            }
            ScoreRows.Add(row);
        }
    }

    static List<CriterionVm> GetLeafCriteria(CourseVm course, ClassVm? cls, int term)
    {
        var list = new List<CriterionVm>();
        var classId = cls?.Id;

        void Walk(IEnumerable<CriterionVm> nodes, string? inheritedClassId, int? inheritedTerm)
        {
            foreach (var n in nodes)
            {
                var effectiveClassId = string.IsNullOrWhiteSpace(n.ClassId) ? inheritedClassId : n.ClassId;
                var effectiveTerm = n.Term ?? inheritedTerm;
                if (n.Children.Count == 0)
                {
                    if (IsMatchingScope(effectiveClassId, classId) && IsMatchingTerm(effectiveTerm, term))
                    {
                        list.Add(n);
                    }
                }
                else
                {
                    Walk(n.Children, effectiveClassId, effectiveTerm);
                }
            }
        }
        Walk(course.Criteria, null, null);
        return list;
    }

    static bool IsMatchingScope(string? criterionClassId, string? selectedClassId)
    {
        if (string.IsNullOrWhiteSpace(criterionClassId))
        {
            return true;
        }

        return string.Equals(criterionClassId, selectedClassId, StringComparison.Ordinal);
    }

    static bool IsMatchingTerm(int? criterionTerm, int selectedTerm)
    {
        if (!criterionTerm.HasValue)
        {
            return selectedTerm == 1;
        }

        return criterionTerm.Value == selectedTerm;
    }
}
