using System;
using ReactiveUI;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public class DynamicAssessment : ReactiveObject
{
    private string studentId;
    private string criterionId;
    private int? term;
    private double? score;

    public DynamicAssessment(string studentId, string criterionId, int? term, double? score)
    {
        this.studentId = studentId;
        this.criterionId = criterionId;
        this.term = term;
        this.score = score;
    }

    public string StudentId => studentId;
    public string CriterionId => criterionId;
    public int? Term => term;

    public double? Score
    {
        get => score;
        set => this.RaiseAndSetIfChanged(ref score, value);
    }

    public AssessmentKey Key => new(studentId, criterionId, term);

    public event Action<DynamicAssessment, AssessmentKey>? KeyChanged;

    internal void SetStudentId(string newStudentId)
    {
        if (studentId == newStudentId)
        {
            return;
        }
        var previous = Key;
        this.RaiseAndSetIfChanged(ref studentId, newStudentId, nameof(StudentId));
        KeyChanged?.Invoke(this, previous);
    }

    internal void SetCriterionId(string newCriterionId)
    {
        if (criterionId == newCriterionId)
        {
            return;
        }
        var previous = Key;
        this.RaiseAndSetIfChanged(ref criterionId, newCriterionId, nameof(CriterionId));
        KeyChanged?.Invoke(this, previous);
    }

    internal void SetTerm(int? newTerm)
    {
        if (term == newTerm)
        {
            return;
        }
        var previous = Key;
        this.RaiseAndSetIfChanged(ref term, newTerm, nameof(Term));
        KeyChanged?.Invoke(this, previous);
    }

    public Assessment ToDomain()
    {
        return new Assessment
        {
            StudentId = studentId,
            CriterionId = criterionId,
            Term = term,
            Score = score
        };
    }
}
