using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using EvaluacionesApp.Desktop.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Dynamic;

public partial class DynamicClass : ReactiveObject, IDisposable
{
    private readonly SourceCache<DynamicStudent, string> studentsCache = new(student => student.Id);
    private readonly SourceCache<DynamicAssessment, AssessmentKey> assessmentsCache = new(assessment => assessment.Key);
    private readonly CompositeDisposable anchors = new();
    private readonly Dictionary<DynamicAssessment, IDisposable> assessmentSubscriptions = new();

    private readonly string id;

    [Reactive]
    private string name;

    public DynamicClass(Class model, DynamicCourse owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        id = model.Id;
        name = model.Name;

        studentsCache.Connect()
            .AutoRefresh(student => student.LastName)
            .AutoRefresh(student => student.FirstName)
            .Sort(Comparer<DynamicStudent>.Create((left, right) =>
            {
                var lastNameComparison = string.Compare(left.LastName, right.LastName, StringComparison.CurrentCultureIgnoreCase);
                if (lastNameComparison != 0)
                {
                    return lastNameComparison;
                }

                return string.Compare(left.FirstName, right.FirstName, StringComparison.CurrentCultureIgnoreCase);
            }))
            .Bind(out ReadOnlyObservableCollection<DynamicStudent> students)
            .Subscribe()
            .DisposeWith(anchors);
        Students = students;

        assessmentsCache.Connect()
            .Bind(out ReadOnlyObservableCollection<DynamicAssessment> assessments)
            .Subscribe()
            .DisposeWith(anchors);
        Assessments = assessments;

        studentsCache.Connect()
            .Subscribe(_ => this.RaisePropertyChanged(nameof(StudentCount)))
            .DisposeWith(anchors);

        foreach (var s in model.Students)
        {
            var student = new DynamicStudent(s);
            studentsCache.AddOrUpdate(student);
        }

        foreach (var a in model.Assessments)
        {
            var assessment = new DynamicAssessment(a.StudentId ?? string.Empty, a.CriterionId ?? string.Empty, a.Score);
            RegisterAssessment(assessment);
            assessmentsCache.AddOrUpdate(assessment);
        }
    }

    public string Id => id;

    public ReadOnlyObservableCollection<DynamicStudent> Students { get; }

    public ReadOnlyObservableCollection<DynamicAssessment> Assessments { get; }

    public IObservable<IChangeSet<DynamicStudent, string>> StudentsChanges => studentsCache.Connect();

    public IObservable<IChangeSet<DynamicAssessment, AssessmentKey>> AssessmentsChanges => assessmentsCache.Connect();

    public int StudentCount => Students.Count;

    public DynamicStudent AddStudent(Student model)
    {
        var student = new DynamicStudent(model);
        studentsCache.AddOrUpdate(student);
        return student;
    }

    public void RemoveStudent(DynamicStudent student)
    {
        studentsCache.RemoveKey(student.Id);
        // Remove assessments referencing this student
        var toRemove = Assessments.Where(a => a.StudentId == student.Id).ToList();
        foreach (var assessment in toRemove)
        {
            RemoveAssessment(assessment);
        }
    }

    public DynamicAssessment GetOrCreateAssessment(string studentId, string criterionId)
    {
        var key = new AssessmentKey(studentId, criterionId);
        if (assessmentsCache.Lookup(key).HasValue)
        {
            return assessmentsCache.Lookup(key).Value;
        }

        var assessment = new DynamicAssessment(studentId, criterionId, null);
        RegisterAssessment(assessment);
        assessmentsCache.AddOrUpdate(assessment);
        return assessment;
    }

    public void RemoveAssessment(DynamicAssessment assessment)
    {
        if (assessmentSubscriptions.Remove(assessment, out var disposable))
        {
            disposable.Dispose();
        }
        assessmentsCache.RemoveKey(assessment.Key);
    }

    public Class ToDomain()
    {
        return new Class
        {
            Id = Id,
            Name = Name,
            Students = Students.Select(s => s.ToDomain()).ToList(),
            Assessments = Assessments.Select(a => a.ToDomain()).ToList()
        };
    }

    private void RegisterAssessment(DynamicAssessment assessment)
    {
        void Handler(DynamicAssessment sender, AssessmentKey previousKey)
        {
            assessmentsCache.Edit(cache =>
            {
                cache.RemoveKey(previousKey);
                cache.AddOrUpdate(sender);
            });
        }

        assessment.KeyChanged += Handler;
        assessmentSubscriptions[assessment] = Disposable.Create(() => assessment.KeyChanged -= Handler);
    }

    public void Dispose()
    {
        foreach (var subscription in assessmentSubscriptions.Values)
        {
            subscription.Dispose();
        }
        assessmentSubscriptions.Clear();

        anchors.Dispose();
    }
}
