using System;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Dynamic;

public partial class DynamicAssessment : ReactiveObject
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private string studentId;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string criterionId;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int? term;

    [Reactive]
    private double? score;

    public DynamicAssessment(string studentId, string criterionId, int? term, double? score)
    {
        this.studentId = studentId;
        this.criterionId = criterionId;
        this.term = term;
        this.score = score;
    }

    public AssessmentKey Key => new(StudentId, CriterionId, Term);

    public event Action<DynamicAssessment, AssessmentKey>? KeyChanged;

    internal void SetStudentId(string newStudentId)
    {
        if (StudentId == newStudentId)
        {
            return;
        }
        var previous = Key;
        StudentId = newStudentId;
        KeyChanged?.Invoke(this, previous);
    }

    internal void SetCriterionId(string newCriterionId)
    {
        if (CriterionId == newCriterionId)
        {
            return;
        }
        var previous = Key;
        CriterionId = newCriterionId;
        KeyChanged?.Invoke(this, previous);
    }

    internal void SetTerm(int? newTerm)
    {
        if (Term == newTerm)
        {
            return;
        }
        var previous = Key;
        Term = newTerm;
        KeyChanged?.Invoke(this, previous);
    }

    public Assessment ToDomain()
    {
        return new Assessment
        {
            StudentId = StudentId,
            CriterionId = CriterionId,
            Term = Term,
            Score = Score
        };
    }
}
