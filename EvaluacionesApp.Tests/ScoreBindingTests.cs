using System.Reactive.Concurrency;
using System.Globalization;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Features.Grades;
using EvaluacionesApp.Desktop.Features.Grades.Converters;
using Xunit;

namespace EvaluacionesApp.Tests;

public class ScoreBindingTests
{
    [Fact]
    public void Reflects_assessment_value_changes()
    {
        var assessment = new DynamicAssessment("stu-1", "crit-1", null);
        var binding = new ScoreBinding(assessment, ImmediateScheduler.Instance);

        Assert.Null(binding.Value);

        binding.Value = 8m;
        Assert.Equal(8m, assessment.Score);

        assessment.Score = 5m;
        Assert.Equal(5m, binding.Value);
    }

    [Fact]
    public void Nullable_decimal_text_converter_treats_empty_text_as_no_score()
    {
        var converter = new NullableDecimalTextConverter();

        var sourceValue = converter.ConvertBack(string.Empty, typeof(decimal?), null, CultureInfo.InvariantCulture);
        var targetValue = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Null(sourceValue);
        Assert.Equal(string.Empty, targetValue);
    }
}
