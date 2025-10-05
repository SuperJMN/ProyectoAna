using System.Reactive.Concurrency;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Views.Grades;
using Xunit;

namespace EvaluacionesApp.Tests;

public class ScoreBindingTests
{
    [Fact]
    public void Reflects_assessment_value_changes()
    {
        var assessment = new DynamicAssessment("stu-1", "crit-1", 1, null);
        var binding = new ScoreBinding(assessment, ImmediateScheduler.Instance);

        Assert.Null(binding.Value);

        binding.Value = 8;
        Assert.Equal(8, assessment.Score);

        assessment.Score = 5;
        Assert.Equal(5, binding.Value);
    }
}
