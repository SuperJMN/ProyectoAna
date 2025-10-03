using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicRoot : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicCourse, string> coursesCache = new(course => course.Id);
    private readonly CompositeDisposable anchors = new();
    private string version;

    public DynamicRoot(Root root)
    {
        version = root.Version;

        coursesCache.Connect()
            .Bind(out ReadOnlyObservableCollection<DynamicCourse> courses)
            .Subscribe()
            .DisposeWith(anchors);
        Courses = courses;

        foreach (var course in root.Courses)
        {
            var dynamicCourse = new DynamicCourse(course, this);
            coursesCache.AddOrUpdate(dynamicCourse);
        }
    }

    public string Version
    {
        get => version;
        set => this.RaiseAndSetIfChanged(ref version, value);
    }

    public ReadOnlyObservableCollection<DynamicCourse> Courses { get; }

    public IObservable<IChangeSet<DynamicCourse, string>> CoursesChanges => coursesCache.Connect();

    public DynamicCourse AddCourse(Course model)
    {
        var course = new DynamicCourse(model, this);
        coursesCache.AddOrUpdate(course);
        return course;
    }

    public void RemoveCourse(DynamicCourse course)
    {
        coursesCache.RemoveKey(course.Id);
        course.Dispose();
    }

    public Root ToDomain()
    {
        return new Root
        {
            Version = Version,
            Courses = Courses.Select(c => c.ToDomain()).ToList()
        };
    }

    public void Dispose()
    {
        foreach (var course in Courses.ToList())
        {
            course.Dispose();
        }

        anchors.Dispose();
    }
}
