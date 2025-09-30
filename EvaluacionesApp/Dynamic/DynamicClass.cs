using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicClass : ReactiveObject, IDisposable
{
    private readonly DynamicCourse owner;
    private readonly SourceCache<DynamicStudent, string> studentsCache = new(student => student.Id);
    private readonly SourceCache<DynamicAssessment, AssessmentKey> assessmentsCache = new(assessment => assessment.Key);
    private readonly CompositeDisposable anchors = new();
    private readonly Dictionary<DynamicStudent, IDisposable> studentSubscriptions = new();
    private readonly Dictionary<DynamicAssessment, IDisposable> assessmentSubscriptions = new();

    private string id;
    private string name;

    public DynamicClass(Class model, DynamicCourse owner)
    {
        this.owner = owner;
        id = model.Id;
        name = model.Name;

        studentsCache.Connect()
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
            RegisterStudent(student);
            studentsCache.AddOrUpdate(student);
        }

        foreach (var a in model.Assessments)
        {
            var assessment = new DynamicAssessment(a.StudentId ?? string.Empty, a.CriterionId ?? string.Empty, a.Term, a.Score);
            RegisterAssessment(assessment);
            assessmentsCache.AddOrUpdate(assessment);
        }
    }

    public string Id
    {
        get => id;
        set
        {
            if (value == id)
            {
                return;
            }
            var previous = id;
            this.RaiseAndSetIfChanged(ref id, value);
            IdChanged?.Invoke(this, previous);
        }
    }

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public ReadOnlyObservableCollection<DynamicStudent> Students { get; }

    public ReadOnlyObservableCollection<DynamicAssessment> Assessments { get; }

    public IObservable<IChangeSet<DynamicStudent, string>> StudentsChanges => studentsCache.Connect();

    public IObservable<IChangeSet<DynamicAssessment, AssessmentKey>> AssessmentsChanges => assessmentsCache.Connect();

    public int StudentCount => Students.Count;

    public event Action<DynamicClass, string>? IdChanged;

    public DynamicStudent AddStudent(Student model)
    {
        var student = new DynamicStudent(model);
        RegisterStudent(student);
        studentsCache.AddOrUpdate(student);
        return student;
    }

    public void RemoveStudent(DynamicStudent student)
    {
        if (studentSubscriptions.Remove(student, out var disposable))
        {
            disposable.Dispose();
        }
        studentsCache.RemoveKey(student.Id);
        // Remove assessments referencing this student
        var toRemove = Assessments.Where(a => a.StudentId == student.Id).ToList();
        foreach (var assessment in toRemove)
        {
            RemoveAssessment(assessment);
        }
    }

    public DynamicAssessment GetOrCreateAssessment(string studentId, string criterionId, int? term)
    {
        var key = new AssessmentKey(studentId, criterionId, term);
        if (assessmentsCache.Lookup(key).HasValue)
        {
            return assessmentsCache.Lookup(key).Value;
        }

        var assessment = new DynamicAssessment(studentId, criterionId, term, null);
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

    internal void UpdateAssessmentsForStudent(string previousStudentId, string newStudentId)
    {
        foreach (var assessment in Assessments.Where(a => a.StudentId == previousStudentId).ToList())
        {
            assessment.SetStudentId(newStudentId);
        }
    }

    internal void UpdateAssessmentsForCriterion(string previousCriterionId, string newCriterionId)
    {
        foreach (var assessment in Assessments.Where(a => a.CriterionId == previousCriterionId).ToList())
        {
            assessment.SetCriterionId(newCriterionId);
        }
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

    private void RegisterStudent(DynamicStudent student)
    {
        void Handler(DynamicStudent sender, string previousId)
        {
            studentsCache.Edit(cache =>
            {
                cache.RemoveKey(previousId);
                cache.AddOrUpdate(sender);
            });
            UpdateAssessmentsForStudent(previousId, sender.Id);
        }

        student.IdChanged += Handler;
        studentSubscriptions[student] = Disposable.Create(() => student.IdChanged -= Handler);
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
        foreach (var subscription in studentSubscriptions.Values)
        {
            subscription.Dispose();
        }
        studentSubscriptions.Clear();

        foreach (var subscription in assessmentSubscriptions.Values)
        {
            subscription.Dispose();
        }
        assessmentSubscriptions.Clear();

        anchors.Dispose();
    }
}
