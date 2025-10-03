using System;
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

namespace EvaluacionesApp.Views.Maintenance;

public partial class StudentsViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<DynamicCourse> EmptyCourses = new(new ObservableCollection<DynamicCourse>());

    private readonly DynamicSchoolStore store;
    private readonly CompositeDisposable anchors = new();
    private CompositeDisposable? courseAnchors;
    private CompositeDisposable? classAnchors;
    private DynamicRoot? root;

    private ReadOnlyObservableCollection<DynamicCourse> courses = EmptyCourses;
    public ReadOnlyObservableCollection<DynamicCourse> Courses
    {
        get => courses;
        private set => this.RaiseAndSetIfChanged(ref courses, value);
    }

    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
    [Reactive] private DynamicStudent? selectedStudent;

    public ReactiveCommand<Unit, Unit> AddStudent { get; }
    public ReactiveCommand<Unit, Unit> DeleteStudent { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }

    public StudentsViewModel(DynamicSchoolStore store)
    {
        this.store = store;
        var canAdd = this.WhenAnyValue(x => x.SelectedClass).Select(c => c != null);
        var canDelete = this.WhenAnyValue(x => x.SelectedStudent).Select(s => s != null);

        AddStudent = ReactiveCommand.CreateFromTask(DoAddStudent, canAdd);
        DeleteStudent = ReactiveCommand.CreateFromTask(DoDeleteStudent, canDelete);
        Save = ReactiveCommand.CreateFromTask(ExecuteSave);
        _ = Load();
    }

    async Task Load()
    {
        root = await store.GetRoot();
        Courses = root.Courses;
        SelectedCourse = Courses.FirstOrDefault();
        SelectedClass = SelectedCourse?.Classes.FirstOrDefault();
        SelectedStudent = SelectedClass?.Students.FirstOrDefault();

        this.WhenAnyValue(x => x.SelectedCourse)
            .Subscribe(HandleSelectedCourseChanged)
            .DisposeWith(anchors);

        this.WhenAnyValue(x => x.SelectedClass)
            .Subscribe(HandleSelectedClassChanged)
            .DisposeWith(anchors);
    }

    void HandleSelectedCourseChanged(DynamicCourse? course)
    {
        courseAnchors?.Dispose();
        courseAnchors = null;

        if (course == null)
        {
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
        SelectedStudent = SelectedClass?.Students.FirstOrDefault();
    }

    void HandleSelectedClassChanged(DynamicClass? cls)
    {
        classAnchors?.Dispose();
        classAnchors = null;

        if (cls == null)
        {
            return;
        }

        classAnchors = new CompositeDisposable();

        cls.StudentsChanges
            .AutoRefresh(s => s.Name)
            .AutoRefresh(s => s.Id)
            .AutoRefresh(s => s.Positivos)
            .AutoRefresh(s => s.Negativos)
            .AutoRefresh(s => s.Observaciones)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default)
            .InvokeCommand(Save)
            .DisposeWith(classAnchors);

        SelectedStudent = cls.Students.FirstOrDefault();
    }

    async Task DoAddStudent()
    {
        if (SelectedClass == null)
        {
            return;
        }

        var idx = SelectedClass.Students.Count + 1;
        var id = Guid.NewGuid().ToString();
        var model = new Models.Student { Id = id, Name = $"Student {idx}" };
        var student = SelectedClass.AddStudent(model);
        SelectedStudent = student;
        await ExecuteSave();
    }

    async Task DoDeleteStudent()
    {
        if (SelectedClass == null || SelectedStudent == null)
        {
            return;
        }

        SelectedClass.RemoveStudent(SelectedStudent);
        SelectedStudent = null;
        await ExecuteSave();
    }

    async Task ExecuteSave()
    {
        await store.SaveAsync();
    }

    public void Dispose()
    {
        classAnchors?.Dispose();
        courseAnchors?.Dispose();
        anchors.Dispose();
    }
}
