using System;
using System.Collections.Generic;
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
    private readonly Dictionary<DynamicCourse, IDisposable> courseSubscriptions = new();

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
            RegisterCourse(dynamicCourse);
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
        RegisterCourse(course);
        coursesCache.AddOrUpdate(course);
        return course;
    }

    public void RemoveCourse(DynamicCourse course)
    {
        if (courseSubscriptions.Remove(course, out var disposer))
        {
            disposer.Dispose();
        }
        coursesCache.RemoveKey(course.Id);
        course.Dispose();
    }

    internal void OnCourseIdChanged(DynamicCourse course, string previousId)
    {
        coursesCache.Edit(cache =>
        {
            cache.RemoveKey(previousId);
            cache.AddOrUpdate(course);
        });
    }

    public Root ToDomain()
    {
        return new Root
        {
            Version = Version,
            Courses = Courses.Select(c => c.ToDomain()).ToList()
        };
    }

    private void RegisterCourse(DynamicCourse course)
    {
        void Handler(DynamicCourse sender, string previous)
        {
            OnCourseIdChanged(sender, previous);
        }

        course.IdChanged += Handler;
        courseSubscriptions[course] = Disposable.Create(() => course.IdChanged -= Handler);
    }

    public void Dispose()
    {
        foreach (var subscription in courseSubscriptions.Values)
        {
            subscription.Dispose();
        }
        courseSubscriptions.Clear();

        foreach (var course in Courses.ToList())
        {
            course.Dispose();
        }

        anchors.Dispose();
    }
}
