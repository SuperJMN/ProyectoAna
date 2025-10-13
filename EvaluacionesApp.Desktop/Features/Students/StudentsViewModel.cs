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
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Desktop.Persistence;

namespace EvaluacionesApp.Desktop.Features.Students;

public partial class StudentsViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<MenuViewModel> EmptyMenuItems = new(new ObservableCollection<MenuViewModel>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<CourseMoveTarget, string> courseMoveTargetsCache = new(target => target.CourseId);
    private readonly SourceCache<MenuViewModel, string> moveMenuCache = new(menu => menu.Key);
    private readonly Dictionary<DynamicCourse, IDisposable> courseSubscriptions = new();
    private readonly Dictionary<DynamicCourse, CourseMoveTarget> courseMoveTargetsByCourse = new();
    private readonly Dictionary<CourseMoveTarget, CourseMoveMenuViewModel> moveMenusByTarget = new();
    private readonly SelectionModel<DynamicStudent> studentsSelection = new() { SingleSelect = false };
    private CompositeDisposable? courseAnchors;
    private CompositeDisposable? classAnchors;
    private DynamicRoot? root;
    private IReadOnlyList<DynamicStudent> selectedStudents = Array.Empty<DynamicStudent>();

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    private ReadOnlyObservableCollection<MenuViewModel> moveStudentsMenu = EmptyMenuItems;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private DynamicStudent? selectedStudent;
    [Reactive] private bool hasSelectedStudents;

    public SelectionModel<DynamicStudent> StudentsSelection => studentsSelection;

    public IReadOnlyList<DynamicStudent> SelectedStudents => selectedStudents;

    public ReadOnlyObservableCollection<MenuViewModel> MoveStudentsMenu => moveStudentsMenu;

    public ReactiveCommand<Unit, Unit> AddStudent { get; }
    public ReactiveCommand<Unit, Unit> DeleteStudent { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<ClassMoveTarget?, Unit> MoveStudents { get; }

    public StudentsViewModel(DynamicSchoolStore store)
    {
        this.store = store;

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
        MoveStudents = ReactiveCommand.CreateFromTask<ClassMoveTarget?>(DoMoveStudents, canMove);
        moveMenuCache.Connect()
            .DisposeMany()
            .AutoRefresh(menu => ((CourseMoveMenuViewModel)menu).CourseOrder)
            .AutoRefresh(menu => menu.Header)
            .Sort(SortExpressionComparer<MenuViewModel>
                .Ascending(menu => ((CourseMoveMenuViewModel)menu).CourseOrder)
                .ThenByAscending(menu => menu.Header))
            .Bind(out moveStudentsMenu)
            .Subscribe()
            .DisposeWith(anchors);
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
        var menu = new CourseMoveMenuViewModel(target, MoveStudents);
        moveMenusByTarget[target] = menu;
        moveMenuCache.AddOrUpdate(menu);
        return target;
    }

    void RemoveCourseMoveTarget(DynamicCourse course)
    {
        if (!courseMoveTargetsByCourse.Remove(course, out var target))
        {
            return;
        }

        RemoveCourseMenu(target);
        courseMoveTargetsCache.RemoveKey(course.Id);
        target.Dispose();
    }

    void RemoveCourseMenu(CourseMoveTarget target)
    {
        if (!moveMenusByTarget.Remove(target, out var menu))
        {
            return;
        }

        moveMenuCache.RemoveKey(menu.Key);
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        classAnchors?.Dispose();
        classAnchors = null;

        if (course == null)
        {
            SelectedClass = null;
            return;
        }

        classAnchors = new CompositeDisposable();
        course.ClassesChanges
            .AutoRefresh(c => c.Name)
            .AutoRefresh(c => c.Id)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(classAnchors);

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

    async Task DoAddStudent()
    {
        if (SelectedClass == null)
        {
            return;
        }

        var idx = SelectedClass.Students.Count + 1;
var model = new Student { Id = $"student-{idx}", FirstName = $"Student {idx}" };
        var student = SelectedClass.AddStudent(model);
        SelectedStudent = student;
        await ExecuteSave();
    }

    async Task DoDeleteStudent()
    {
        var studentsToDelete = SelectedStudents.ToList();
        if (studentsToDelete.Count == 0 || SelectedClass == null)
        {
            return;
        }

        foreach (var student in studentsToDelete)
        {
            SelectedClass.RemoveStudent(student);
        }

        SelectedStudent = SelectedClass.Students.FirstOrDefault();
        await ExecuteSave();
    }

    async Task DoMoveStudents(ClassMoveTarget? target)
    {
        if (target == null || SelectedClass == null || SelectedCourse == null)
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

    private sealed class CourseMoveMenuViewModel : MenuViewModel, IDisposable
    {
        private readonly CompositeDisposable anchors = new();
        private int courseOrder;

        public CourseMoveMenuViewModel(CourseMoveTarget target, ReactiveCommand<ClassMoveTarget?, Unit> command)
        {
            Key = target.CourseId;
            Header = target.CourseName;
            CourseOrder = target.CourseOrder;

            target.WhenAnyValue(x => x.CourseName)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(value => Header = value)
                .DisposeWith(anchors);

            target.WhenAnyValue(x => x.CourseOrder)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(value => CourseOrder = value)
                .DisposeWith(anchors);

            target.Classes
                .ToObservableChangeSet()
                .Select(changes => changes.Transform(classTarget => (MenuItemViewModel)new ClassMoveMenuItemViewModel(classTarget, command)))
                .DisposeMany()
                .Bind(out var classItems)
                .Subscribe()
                .DisposeWith(anchors);

            SetChildren(classItems);
        }

        public int CourseOrder
        {
            get => courseOrder;
            private set => this.RaiseAndSetIfChanged(ref courseOrder, value);
        }

        public void Dispose()
        {
            anchors.Dispose();
        }
    }

    private sealed class ClassMoveMenuItemViewModel : MenuItemViewModel, IDisposable
    {
        private readonly CompositeDisposable anchors = new();
        private readonly ClassMoveTarget target;

        public ClassMoveMenuItemViewModel(ClassMoveTarget target, ReactiveCommand<ClassMoveTarget?, Unit> command)
        {
            this.target = target;
            Key = target.Key;
            Command = command;
            CommandParameter = target;
            Header = target.ClassName;

            target.WhenAnyValue(x => x.ClassName)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(value => Header = value)
                .DisposeWith(anchors);
        }

        public void Dispose()
        {
            anchors.Dispose();
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
        moveMenuCache.Dispose();
        moveMenusByTarget.Clear();
    }

    void HandleCourseClassChanges(DynamicCourse course, IChangeSet<DynamicClass, string> changes)
    {
        var target = GetOrCreateCourseMoveTarget(course);
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    target.AddOrUpdateClass(change.Current);
                    break;
                case ChangeReason.Remove:
                    target.RemoveClass(change.Current);
                    break;
            }
        }
    }
}

public sealed class CourseMoveTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<ClassMoveTarget, string> classesCache = new(target => target.Key);
    private readonly ReadOnlyObservableCollection<ClassMoveTarget> classes;
    private string courseName = string.Empty;
    private int courseOrder;

    public CourseMoveTarget(DynamicCourse course)
    {
        Course = course;

        classesCache.Connect()
            .OnItemRemoved(DisposeClassTarget)
            .AutoRefresh(target => target.ClassName)
            .Sort(SortExpressionComparer<ClassMoveTarget>
                .Ascending(target => target.ClassName))
            .Bind(out classes)
            .Subscribe()
            .DisposeWith(anchors);

        UpdateState();

        course.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.WhenAnyValue(x => x.Number)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);
    }

    public DynamicCourse Course { get; }

    public string CourseId => Course.Id;

    public string CourseName => courseName;

    public int CourseOrder => courseOrder;

    public ReadOnlyObservableCollection<ClassMoveTarget> Classes => classes;

    public bool IsEmpty => classes.Count == 0;

    public void AddOrUpdateClass(DynamicClass cls)
    {
        var key = ClassMoveTarget.BuildKey(Course, cls);
        var existing = classesCache.Lookup(key);
        if (existing.HasValue)
        {
            if (!ReferenceEquals(existing.Value.Class, cls))
            {
                classesCache.RemoveKey(key);
                classesCache.AddOrUpdate(new ClassMoveTarget(Course, cls));
            }

            return;
        }

        classesCache.AddOrUpdate(new ClassMoveTarget(Course, cls));
    }

    public void RemoveClass(DynamicClass cls)
    {
        classesCache.RemoveKey(ClassMoveTarget.BuildKey(Course, cls));
    }

    void DisposeClassTarget(ClassMoveTarget target)
    {
        target.Dispose();
    }

    void UpdateState()
    {
        var newName = Course.Name;
        if (newName != courseName)
        {
            courseName = newName;
            this.RaisePropertyChanged(nameof(CourseName));
        }

        var newOrder = Course.Number ?? int.MaxValue;
        if (newOrder != courseOrder)
        {
            courseOrder = newOrder;
            this.RaisePropertyChanged(nameof(CourseOrder));
        }
    }

    public void Dispose()
    {
        anchors.Dispose();

        foreach (var target in classesCache.Items.ToList())
        {
            target.Dispose();
        }

        classesCache.Dispose();
    }
}

public sealed class ClassMoveTarget : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable anchors = new();
    private string className = string.Empty;

    public ClassMoveTarget(DynamicCourse course, DynamicClass cls)
    {
        Course = course;
        Class = cls;
        UpdateState();

        cls.WhenAnyValue(x => x.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);
    }

    public DynamicClass Class { get; }
    public DynamicCourse Course { get; }

    public string CourseName => Course.Name;

    public string ClassName
    {
        get => className;
        private set => this.RaiseAndSetIfChanged(ref className, value);
    }

    public string Key => BuildKey(Course, Class);

    public static string BuildKey(DynamicCourse course, DynamicClass cls) => $"{course.Id}:{cls.Id}";

    void UpdateState()
    {
        ClassName = Class.Name;
    }

    public void Dispose()
    {
        anchors.Dispose();
    }
}
