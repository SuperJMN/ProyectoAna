using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Desktop.Models;
using EvaluacionesApp.Desktop.Services;

namespace EvaluacionesApp.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<CourseVm> Courses { get; } = new();

    [Reactive]
    private CourseVm? selectedCourse;

    [Reactive]
    private ClassVm? selectedClass;

    public ObservableCollection<ScoreRowVm> ScoreRows { get; } = new();

    public ObservableCollection<int> Terms { get; } = new();

    [Reactive]
    private int selectedTerm = 1;

    public IReadOnlyList<CriterionVm> LeafCriteria =>
        SelectedCourse == null ? Array.Empty<CriterionVm>() : GetLeafCriteria(SelectedCourse, SelectedTerm);

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

    readonly NotifyCollectionChangedEventHandler termCollectionChanged;

    public MainWindowViewModel()
    {
        termCollectionChanged = (_, _) =>
        {
            UpdateTerms();
            EnsureSelectedTermExists();
            BuildScoreRows();
            this.RaisePropertyChanged(nameof(LeafCriteria));
        };

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

        UpdateTerms();
        EnsureSelectedTermExists();

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
            var criterionTerms = BuildCriterionTermLookup(SelectedCourse);
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
                            Score = v
                        });
                    }
                }
            }
            // Update backing model for the selected class
            var model = SelectedClass.Model;
            model.Assessments = AssessmentUtilities.MergeAssessments(
                model.Assessments,
                updates,
                criterionTerms,
                SelectedTerm);
        }

        var root = new Root();
        root.Courses = Courses.Select(c => c.ToModel()).ToList();
        await persistence.Save(root);
    }

    void DoAddCourse()
    {
        var idx = Courses.Count + 1;
        var defaultTerms = Terms.Count > 0 ? Terms.ToList() : new List<int> { 1, 2, 3 };
        var model = new Course
        {
            Id = $"course-{idx}",
            Name = $"Course {idx}",
            Terms = defaultTerms
        };
        var vm = new CourseVm(model);
        Courses.Add(vm);
        SelectedCourse = vm;
        UpdateTerms();
        EnsureSelectedTermExists();
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
        var model = new Student
        {
            Id = id,
            FirstName = "Student",
            LastName = idx.ToString()
        };
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
            ClassId = string.Empty,
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
            ClassId = string.IsNullOrWhiteSpace(parent.ClassId) ? string.Empty : parent.ClassId,
            Term = parent.Term
        };
        parent.Children.Add(new CriterionVm(model));
        BuildScoreRows();
        this.RaisePropertyChanged(nameof(LeafCriteria));
    }

    void HookSelectionSubscriptions()
    {
        CourseVm? previousCourse = null;

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(value =>
            {
                if (previousCourse != null)
                {
                    previousCourse.Terms.CollectionChanged -= termCollectionChanged;
                }

                if (value != null)
                {
                    value.Terms.CollectionChanged += termCollectionChanged;
                }

                previousCourse = value;

                SelectedClass = value?.Classes.FirstOrDefault();
                UpdateTerms();
                EnsureSelectedTermExists();
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

        var leafCriteria = GetLeafCriteria(SelectedCourse, SelectedTerm);
        var criterionTerms = BuildCriterionTermLookup(SelectedCourse);
        var existing = AssessmentUtilities.BuildScoreLookup(SelectedClass.Model.Assessments, criterionTerms, SelectedTerm);

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

    static Dictionary<string, int?> BuildCriterionTermLookup(CourseVm course)
    {
        var map = new Dictionary<string, int?>();

        void Walk(IEnumerable<CriterionVm> nodes, int? inheritedTerm)
        {
            foreach (var node in nodes)
            {
                var effectiveTerm = node.Term ?? inheritedTerm;
                map[node.Id] = effectiveTerm;

                if (node.Children.Count > 0)
                {
                    Walk(node.Children, effectiveTerm);
                }
            }
        }

        Walk(course.Criteria, null);
        return map;
    }

    static List<CriterionVm> GetLeafCriteria(CourseVm course, int term)
    {
        var list = new List<CriterionVm>();

        void Walk(IEnumerable<CriterionVm> nodes, int? inheritedTerm)
        {
            foreach (var n in nodes)
            {
                var effectiveTerm = n.Term ?? inheritedTerm;
                if (n.Children.Count == 0)
                {
                    if (IsMatchingTerm(effectiveTerm, term))
                    {
                        list.Add(n);
                    }
                }
                else
                {
                    Walk(n.Children, effectiveTerm);
                }
            }
        }
        Walk(course.Criteria, null);
        return list;
    }

    static bool IsMatchingTerm(int? criterionTerm, int selectedTerm)
    {
        if (!criterionTerm.HasValue)
        {
            return selectedTerm == 1;
        }

        return criterionTerm.Value == selectedTerm;
    }

    void UpdateTerms()
    {
        Terms.Clear();
        if (SelectedCourse == null)
        {
            Terms.Add(SelectedTerm);
            return;
        }

        IEnumerable<int> source = SelectedCourse.Terms.Count > 0
            ? SelectedCourse.Terms
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
    }

    void EnsureSelectedTermExists()
    {
        if (!Terms.Contains(SelectedTerm))
        {
            SelectedTerm = Terms.First();
        }
    }
}
