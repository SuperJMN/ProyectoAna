using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Selection;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Desktop.Dynamic;

namespace EvaluacionesApp.Desktop.Views.Maintenance;

public partial class StudentsViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<CourseMoveTarget> EmptyCourseMoveTargets = new(new ObservableCollection<CourseMoveTarget>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<CourseMoveTarget, string> courseMoveTargetsCache = new(target => target.CourseId);
    private readonly Dictionary<DynamicCourse, IDisposable> courseSubscriptions = new();
    private readonly Dictionary<DynamicCourse, CourseMoveTarget> courseMoveTargetsByCourse = new();
    private readonly SelectionModel<DynamicStudent> studentsSelection = new() { SingleSelect = false };
    private CompositeDisposable? courseAnchors;
    private CompositeDisposable? classAnchors;
    private DynamicRoot? root;
    private IReadOnlyList<DynamicStudent> selectedStudents = Array.Empty<DynamicStudent>();

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    private ReadOnlyObservableCollection<CourseMoveTarget> courseMoveTargets = EmptyCourseMoveTargets;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private DynamicStudent? selectedStudent;
    [Reactive] private bool hasSelectedStudents;

    public SelectionModel<DynamicStudent> StudentsSelection => studentsSelection;

    public IReadOnlyList<DynamicStudent> SelectedStudents => selectedStudents;

    public ReadOnlyObservableCollection<CourseMoveTarget> CourseMoveTargets => courseMoveTargets;

    public ReactiveCommand<Unit, Unit> AddStudent { get; }
    public ReactiveCommand<Unit, Unit> DeleteStudent { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<ClassMoveTarget, Unit> MoveStudents { get; }

    public StudentsViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        courseMoveTargetsCache.Connect()
            .OnItemRemoved(DisposeCourseMoveTarget)
            .AutoRefresh(target => target.CourseOrder)
            .AutoRefresh(target => target.CourseName)
            .Sort(SortExpressionComparer<CourseMoveTarget>
                .Ascending(target => target.CourseOrder)
                .ThenByAscending(target => target.CourseName))
            .Bind(out courseMoveTargets)
            .Subscribe()
            .DisposeWith(anchors);

        studentsSelection.SelectionChanged += HandleStudentsSelectionChanged;
        anchors.Add(Disposable.Create(() => studentsSelection.SelectionChanged -= HandleStudentsSelectionChanged));

        var canAdd = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);
        var hasSelection = this.WhenAnyValue(x => x.HasSelectedStudents);
        var hasClass = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);
        var canDelete = hasSelection;
        var canMove = hasClass.CombineLatest(hasSelection, (cls, selected) => cls && selected);

        AddStudent = ReactiveCommand.CreateFromTask(DoAddStudent, canAdd);
        DeleteStudent = ReactiveCommand.CreateFromTask(DoDeleteStudent, canDelete);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        MoveStudents = ReactiveCommand.CreateFromTask<ClassMoveTarget>(DoMoveStudents, canMove);
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

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(HandleSelectedClassChanged)
            .DisposeWith(anchors);

        root.CoursesChanges
            .Subscribe(HandleCoursesChanged)
            .DisposeWith(anchors);

        SelectedCourse = Courses.FirstOrDefault();
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

        var courseTarget = GetOrCreateCourseMoveTarget(course);

        foreach (var cls in course.Classes)
        {
            courseTarget.AddOrUpdateClass(cls);
        }

        var subscription = course.ClassesChanges.Subscribe(new ClassChangesObserver(this, course));
        courseSubscriptions[course] = subscription;
    }

    void UnregisterCourse(DynamicCourse course)
    {
        if (courseSubscriptions.Remove(course, out var subscription))
        {
            subscription.Dispose();
        }

        RemoveCourseMoveTarget(course);
    }

    CourseMoveTarget GetOrCreateCourseMoveTarget(DynamicCourse course)
    {
        if (courseMoveTargetsByCourse.TryGetValue(course, out var existing))
        {
            return existing;
        }

        var target = new CourseMoveTarget(course);
        courseMoveTargetsByCourse[course] = target;
        courseMoveTargetsCache.AddOrUpdate(target);
        return target;
    }

    void RemoveCourseMoveTarget(DynamicCourse course)
    {
        if (!courseMoveTargetsByCourse.Remove(course, out _))
        {
            return;
        }

        courseMoveTargetsCache.RemoveKey(course.Id);
    }

    void DisposeCourseMoveTarget(CourseMoveTarget target)
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
                    GetOrCreateCourseMoveTarget(course).AddOrUpdateClass(change.Current);
                    break;
                case ChangeReason.Remove:
                    RemoveClassFromCourseTarget(course, change.Current);
                    break;
            }
        }
    }

    void RemoveClassFromCourseTarget(DynamicCourse course, DynamicClass cls)
    {
        if (!courseMoveTargetsByCourse.TryGetValue(course, out var courseTarget))
        {
            return;
        }

        courseTarget.RemoveClass(cls);

        if (courseTarget.IsEmpty)
        {
            RemoveCourseMoveTarget(course);
        }
    }

    void HandleStudentsSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<DynamicStudent> _)
    {
        UpdateSelectionSnapshot();
    }

    void SelectStudents(IEnumerable<DynamicStudent> students)
    {
        StudentsSelection.Clear();

        if (SelectedClass == null)
        {
            UpdateSelectionSnapshot();
            return;
        }

        var list = students?.Where(student => student != null).ToList() ?? new List<DynamicStudent>();
        foreach (var student in list)
        {
            var index = SelectedClass.Students.IndexOf(student);
            if (index >= 0)
            {
                StudentsSelection.Select(index);
            }
        }

        if (list.Count == 0 && SelectedClass.Students.Count > 0)
        {
            StudentsSelection.Select(0);
        }

        UpdateSelectionSnapshot();
    }

    void UpdateSelectionSnapshot()
    {
        var snapshot = StudentsSelection.SelectedItems
            .OfType<DynamicStudent>()
            .ToList();
        selectedStudents = snapshot;
        this.RaisePropertyChanged(nameof(SelectedStudents));
        HasSelectedStudents = snapshot.Count > 0;
        SelectedStudent = snapshot.FirstOrDefault();
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;

        if (course == null)
        {
            SelectedClass = null;
            return;
        }

        courseAnchors = new CompositeDisposable();
        course.ClassesChanges
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Id)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(courseAnchors);

        SelectedClass = course.Classes.FirstOrDefault();
    }

    void HandleSelectedClassChanged(DynamicClass? cls)
    {
        classAnchors?.Dispose();
        classAnchors = null;

        StudentsSelection.Source = cls?.Students;

        if (cls == null)
        {
            SelectStudents(Array.Empty<DynamicStudent>());
            return;
        }

        classAnchors = new CompositeDisposable();

        cls.StudentsChanges
            .AutoRefresh(s => s.FirstName)
            .AutoRefresh(s => s.LastName)
            .AutoRefresh(s => s.Id)
            .AutoRefresh(s => s.Positivos)
            .AutoRefresh(s => s.Negativos)
            .AutoRefresh(s => s.Observaciones)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(classAnchors);

        SelectStudents(cls.Students.Take(1));
    }

    async Task DoAddStudent()
    {
        if (SelectedClass == null)
        {
            return;
        }

        var idx = SelectedClass.Students.Count + 1;
        var id = Guid.NewGuid().ToString();
        var model = new Models.Student
        {
            Id = id,
            FirstName = "Student",
            LastName = idx.ToString()
        };
        var student = SelectedClass.AddStudent(model);
        SelectStudents(new[] { student });
        await ExecuteSave();
    }

    async Task DoDeleteStudent()
    {
        if (SelectedClass == null)
        {
            return;
        }

        var studentsToRemove = SelectedStudents.ToList();
        if (studentsToRemove.Count == 0)
        {
            return;
        }

        foreach (var student in studentsToRemove)
        {
            SelectedClass.RemoveStudent(student);
        }

        SelectStudents(SelectedClass.Students.Take(1));
        await ExecuteSave();
    }

    async Task DoMoveStudents(ClassMoveTarget? target)
    {
        if (target == null || SelectedClass == null)
        {
            return;
        }

        var studentsToMove = SelectedStudents.ToList();
        if (studentsToMove.Count == 0)
        {
            return;
        }

        if (ReferenceEquals(target.Class, SelectedClass))
        {
            return;
        }

        var newStudents = new List<DynamicStudent>();
        foreach (var student in studentsToMove)
        {
            SelectedClass.RemoveStudent(student);
            var created = target.Class.AddStudent(student.ToDomain());
            newStudents.Add(created);
        }

        SelectedCourse = target.Course;
        SelectedClass = target.Class;
        SelectStudents(newStudents);

        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    private sealed class ClassChangesObserver : IObserver<IChangeSet<DynamicClass, string>>
    {
        private readonly StudentsViewModel owner;
        private readonly DynamicCourse course;

        public ClassChangesObserver(StudentsViewModel owner, DynamicCourse course)
        {
            this.owner = owner;
            this.course = course;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception _)
        {
        }

        public void OnNext(IChangeSet<DynamicClass, string> value)
        {
            owner.HandleCourseClassChanges(course, value);
        }
    }

    public void Dispose()
    {
        classAnchors?.Dispose();
        courseAnchors?.Dispose();
        anchors.Dispose();
        studentsSelection.SelectionChanged -= HandleStudentsSelectionChanged;

        foreach (var subscription in courseSubscriptions.Values.ToList())
        {
            subscription.Dispose();
        }
        courseSubscriptions.Clear();

        foreach (var target in courseMoveTargetsCache.Items.ToList())
        {
            target.Dispose();
        }
        courseMoveTargetsCache.Dispose();
        courseMoveTargetsByCourse.Clear();
    }
}
