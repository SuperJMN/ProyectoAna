namespace EvaluacionesApp.Desktop.Views.Maintenance;

public sealed class TermCriterionCopyTarget
{
    public TermCriterionCopyTarget(CriterionCopyTarget target)
    {
        Target = target;
        TermName = $"Trimestre {target.Term}";
    }

    public CriterionCopyTarget Target { get; }

    public string TermName { get; }
}
