using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Selection;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Desktop.Persistence;
using Zafiro.Avalonia.Misc;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace EvaluacionesApp.Desktop.Features.Students;

[Section(name: "Students", icon: "mdi-account-group", sortIndex: 4, FriendlyName = "Alumnos")]
public partial class StudentsViewModel : ReactiveValidationObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());
    private static readonly ReadOnlyObservableCollection<MenuViewModel> EmptyMenuItems = new(new ObservableCollection<MenuViewModel>());

    private readonly IDynamicSchoolStore store;
    private readonly INotificationService notifications;
    private readonly SchoolSelectionState selection;
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<CourseMoveTarget, string> courseMoveTargetsCache = new(target => target.CourseId);
    private readonly SourceCache<MenuViewModel, string> moveMenuCache = new(menu => menu.Key);
    private readonly Dictionary<DynamicCourse, IDisposable> courseSubscriptions = new();
    private readonly Dictionary<DynamicCourse, CourseMoveTarget> courseMoveTargetsByCourse = new();
    private readonly Dictionary<CourseMoveTarget, CourseMoveMenuViewModel> moveMenusByTarget = new();
    private CompositeDisposable? courseAnchors;
    private CompositeDisposable? classAnchors;
    private DynamicRoot? root;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;

    private ReadOnlyObservableCollection<MenuViewModel> moveStudentsMenu = EmptyMenuItems;

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<DynamicStudent> compactSelectionStudents = [];
    private DynamicStudent? selectedStudent;
    private bool syncingStudentSelection;
    private bool syncingSchoolSelection;
    [Reactive] private bool isCompactSelectionMode;

    public ReactiveSelection<DynamicStudent, string> StudentsSelection { get; }

    public ReadOnlyObservableCollection<MenuViewModel> MoveStudentsMenu => moveStudentsMenu;

    public ReactiveCommand<Unit, Unit> AddStudent { get; }
    public ReactiveCommand<Unit, Unit> DeleteStudent { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<ClassMoveTarget?, Unit> MoveStudents { get; }
    public ReactiveCommand<Unit, Unit> EnterCompactSelectionMode { get; }
    public ReactiveCommand<Unit, Unit> ExitCompactSelectionMode { get; }

    public DynamicStudent? SelectedStudent
    {
        get => selectedStudent;
        set => SetSelectedStudent(value, true);
    }

    public StudentsViewModel(IDynamicSchoolStore store, INotificationService notifications, SchoolSelectionState? selection = null)
    {
        this.store = store;
        this.notifications = notifications;
        this.selection = selection ?? new SchoolSelectionState();

        StudentsSelection = new ReactiveSelection<DynamicStudent, string>(
            new Avalonia.Controls.Selection.SelectionModel<DynamicStudent> { SingleSelect = false },
            student => student.Id);
        StudentsSelection.DisposeWith(anchors);

        StudentsSelection.SelectedItems
            .ToObservableChangeSet()
            .AutoRefresh()
            .Subscribe(_ =>
            {
                if (!syncingStudentSelection)
                {
                    SetSelectedStudent(StudentsSelection.SelectedItems.FirstOrDefault(), false);
                }
            })
            .DisposeWith(anchors);

        var canAdd = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);
        var hasSelection = this.WhenAnyValue(x => x.StudentsSelection.SelectedItems.Count).Select(count => count > 0);
        var hasClass = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);
        var canDelete = hasSelection;
        var canMove = hasClass.CombineLatest(hasSelection, (cls, selected) => cls && selected);

        AddStudent = ReactiveCommand.CreateFromTask(DoAddStudent, canAdd);
        DeleteStudent = ReactiveCommand.CreateFromTask(DoDeleteStudent, canDelete);
        this.ValidationRule(ObserveSelectedStudentName(), "El nombre del alumno no puede estar vacio");
        Save = ReactiveCommand.CreateFromTask(ExecuteSave, ValidationContext.Valid);
        Save.ThrownExceptions
            .Subscribe(ex => _ = this.notifications.Show("No se pudieron guardar los alumnos", ex.Message))
            .DisposeWith(anchors);
        MoveStudents = ReactiveCommand.CreateFromTask<ClassMoveTarget?>(DoMoveStudents, canMove);
        EnterCompactSelectionMode = ReactiveCommand.Create(EnableCompactSelectionMode);
        ExitCompactSelectionMode = ReactiveCommand.Create(DisableCompactSelectionMode);
        moveMenuCache.Connect()
            .DisposeMany()
            .AutoRefresh(menu => ((CourseMoveMenuViewModel)menu).CourseOrder)
            .AutoRefresh(menu => menu.Header)
            .SortAndBind(out moveStudentsMenu, SortExpressionComparer<MenuViewModel>
                .Ascending(menu => ((CourseMoveMenuViewModel)menu).CourseOrder)
                .ThenByAscending(menu => menu.Header))
            .Subscribe()
            .DisposeWith(anchors);
        Load();
    }

    void Load()
    {
        root = store.Root;
        Courses = root.Courses;
        RegisterExistingCourses(root);

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(HandleSelectedClassChanged)
            .DisposeWith(anchors);

        BindSchoolSelection();

        root.CoursesChanges
            .Subscribe(HandleCoursesChanged)
            .DisposeWith(anchors);

        SelectedCourse = ResolveCourse(selection.SelectedCourse);
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
            .Throttle(TimeSpan.FromMilliseconds(300), RxSchedulers.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(courseAnchors);

        SelectedClass = ResolveClass(course, selection.SelectedClass);
    }

    void HandleSelectedClassChanged(DynamicClass? cls)
    {
        classAnchors?.Dispose();
        classAnchors = null;

        DisableCompactSelectionMode();
        RefreshStudentsSelectionSource();
        SetSelectedStudent(null, false);
        SyncSelectionToSelectedStudent(null);

        if (cls == null)
        {
            return;
        }

        classAnchors = new CompositeDisposable();

        cls.StudentsChanges
            .Subscribe(_ => RefreshStudentsSelectionSource())
            .DisposeWith(classAnchors);

        cls.StudentsChanges
            .AutoRefresh(s => s.FirstName)
            .AutoRefresh(s => s.LastName)
            .AutoRefresh(s => s.Id)
            .AutoRefresh(s => s.Positivos)
            .AutoRefresh(s => s.Negativos)
            .AutoRefresh(s => s.Observaciones)
            .Throttle(TimeSpan.FromMilliseconds(300), RxSchedulers.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(classAnchors);

        // Select first student if available
        if (cls.Students.Count > 0)
        {
            SelectStudent(cls.Students[0]);
        }
    }


    async Task DoAddStudent()
    {
        if (SelectedClass == null)
        {
            return;
        }

        var idx = SelectedClass.Students.Count + 1;
        var model = new Student { Id = $"student-{idx}", FirstName = $"Alumno {idx}" };
        var student = SelectedClass.AddStudent(model);
        RefreshStudentsSelectionSource();
        SelectStudent(student);
        await ExecuteSave();
    }

    void SelectStudent(DynamicStudent student)
    {
        SetSelectedStudent(student, true);
    }

    void SetSelectedStudent(DynamicStudent? student, bool syncSelection)
    {
        if (student is not null && !IsStudentInSelectedClass(student))
        {
            return;
        }

        if (!ReferenceEquals(selectedStudent, student))
        {
            this.RaiseAndSetIfChanged(ref selectedStudent, student, nameof(SelectedStudent));
        }

        if (syncSelection)
        {
            SyncSelectionToSelectedStudent(student);
        }
    }

    bool IsStudentInSelectedClass(DynamicStudent student)
    {
        return SelectedClass?.Students.Contains(student) == true;
    }

    void BindSchoolSelection()
    {
        this.WhenAnyValue(x => x.SelectedCourse)
            .Skip(1)
            .Subscribe(PublishSelectedCourse)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass)
            .Skip(1)
            .Subscribe(PublishSelectedClass)
            .DisposeWith(anchors);

        selection.WhenAnyValue(x => x.SelectedCourse)
            .Skip(1)
            .Subscribe(ApplySelectedCourse)
            .DisposeWith(anchors);

        selection.WhenAnyValue(x => x.SelectedClass)
            .Skip(1)
            .Subscribe(ApplySelectedClass)
            .DisposeWith(anchors);
    }

    void PublishSelectedCourse(DynamicCourse? course)
    {
        if (syncingSchoolSelection)
        {
            return;
        }

        syncingSchoolSelection = true;
        try
        {
            selection.SelectedCourse = course;
        }
        finally
        {
            syncingSchoolSelection = false;
        }
    }

    void PublishSelectedClass(DynamicClass? cls)
    {
        if (syncingSchoolSelection)
        {
            return;
        }

        syncingSchoolSelection = true;
        try
        {
            selection.SelectedClass = cls;

            var owner = cls == null ? null : FindCourse(cls);
            if (owner != null)
            {
                selection.SelectedCourse = owner;
            }
        }
        finally
        {
            syncingSchoolSelection = false;
        }
    }

    void ApplySelectedCourse(DynamicCourse? course)
    {
        var resolved = ResolveCourse(course);
        if (ReferenceEquals(SelectedCourse, resolved))
        {
            return;
        }

        syncingSchoolSelection = true;
        try
        {
            SelectedCourse = resolved;
        }
        finally
        {
            syncingSchoolSelection = false;
        }
    }

    void ApplySelectedClass(DynamicClass? cls)
    {
        if (cls == null)
        {
            return;
        }

        var owner = FindCourse(cls);
        if (owner == null)
        {
            return;
        }

        syncingSchoolSelection = true;
        try
        {
            if (!ReferenceEquals(SelectedCourse, owner))
            {
                SelectedCourse = owner;
            }

            if (!ReferenceEquals(SelectedClass, cls))
            {
                SelectedClass = cls;
            }
        }
        finally
        {
            syncingSchoolSelection = false;
        }
    }

    DynamicCourse? ResolveCourse(DynamicCourse? candidate)
    {
        return candidate != null && Courses.Contains(candidate)
            ? candidate
            : Courses.FirstOrDefault();
    }

    DynamicClass? ResolveClass(DynamicCourse course, DynamicClass? candidate)
    {
        return candidate != null && course.Classes.Contains(candidate)
            ? candidate
            : course.Classes.FirstOrDefault();
    }

    DynamicCourse? FindCourse(DynamicClass cls)
    {
        return Courses.FirstOrDefault(course => course.Classes.Contains(cls));
    }

    void SyncSelectionToSelectedStudent(DynamicStudent? student)
    {
        syncingStudentSelection = true;
        try
        {
            StudentsSelection.SelectionModel.Clear();

            if (student is null || SelectedClass is null)
            {
                return;
            }

            var index = SelectedClass.Students.IndexOf(student);
            if (index >= 0)
            {
                StudentsSelection.SelectionModel.Select(index);
            }
        }
        finally
        {
            syncingStudentSelection = false;
        }
    }

    void RefreshStudentsSelectionSource()
    {
        CompactSelectionStudents = SelectedClass?.Students.ToArray() ?? [];
        StudentsSelection.SelectionModel.Source = CompactSelectionStudents;
    }

    void EnableCompactSelectionMode()
    {
        IsCompactSelectionMode = true;
        StudentsSelection.SelectionModel.SingleSelect = false;
    }

    void DisableCompactSelectionMode()
    {
        IsCompactSelectionMode = false;
        NormalizeSelectionToSingleItem();
        StudentsSelection.SelectionModel.SingleSelect = false;
    }

    void NormalizeSelectionToSingleItem()
    {
        var student = SelectedStudent ?? StudentsSelection.SelectedItems.FirstOrDefault();
        if (student is null)
        {
            return;
        }

        SelectStudent(student);
    }

    async Task DoDeleteStudent()
    {
        var studentsToDelete = StudentsSelection.SelectedItems.ToList();
        if (studentsToDelete.Count == 0 || SelectedClass == null)
        {
            return;
        }

        foreach (var student in studentsToDelete)
        {
            SelectedClass.RemoveStudent(student);
        }

        RefreshStudentsSelectionSource();
        await ExecuteSave();
    }

    async Task DoMoveStudents(ClassMoveTarget? target)
    {
        if (target == null || SelectedClass == null || SelectedCourse == null)
        {
            return;
        }

        var studentsToMove = StudentsSelection.SelectedItems.ToList();
        if (studentsToMove.Count == 0)
        {
            return;
        }

        if (ReferenceEquals(target.Class, SelectedClass))
        {
            return;
        }

        foreach (var student in studentsToMove)
        {
            SelectedClass.RemoveStudent(student);
            target.Class.AddStudent(student.ToDomain());
        }

        SelectedCourse = target.Course;
        SelectedClass = target.Class;
        RefreshStudentsSelectionSource();

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
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(value => Header = value)
                .DisposeWith(anchors);

            target.WhenAnyValue(x => x.CourseOrder)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
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
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(value => Header = value)
                .DisposeWith(anchors);
        }

        public void Dispose()
        {
            anchors.Dispose();
        }
    }

    IObservable<bool> ObserveSelectedStudentName()
    {
        return this.WhenAnyValue(x => x.SelectedStudent)
            .Select(student => student?.WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                (firstName, lastName) => HasText(firstName) || HasText(lastName)) ?? Observable.Return(true))
            .Switch()
            .DistinctUntilChanged();
    }

    static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public new void Dispose()
    {
        classAnchors?.Dispose();
        courseAnchors?.Dispose();
        anchors.Dispose();

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
        base.Dispose();
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
            .SortAndBind(out classes, SortExpressionComparer<ClassMoveTarget>
                .Ascending(target => target.ClassName))
            .Subscribe()
            .DisposeWith(anchors);

        UpdateState();

        course.WhenAnyValue(x => x.Name)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateState())
            .DisposeWith(anchors);

        course.WhenAnyValue(x => x.Number)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
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
            .ObserveOn(RxSchedulers.MainThreadScheduler)
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
