using System;
using EvaluacionesApp.Desktop.Persistence;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.Dynamic;

public partial class DynamicAssessment : ReactiveObject
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private string studentId;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string criterionId;

    [Reactive]
    private decimal? score;

    public DynamicAssessment(string studentId, string criterionId, decimal? score)
    {
        this.studentId = studentId;
        this.criterionId = criterionId;
        this.score = score;
    }

    public AssessmentKey Key => new(StudentId, CriterionId);

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

    public Assessment ToDomain()
    {
        return new Assessment
        {
            StudentId = StudentId,
            CriterionId = CriterionId,
            Score = Score
        };
    }
}
