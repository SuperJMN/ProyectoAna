using EvaluacionesApp.Desktop.Models;
using EvaluacionesApp.Desktop.ViewModels;

namespace EvaluacionesApp.Tests;

public class AssessmentUtilitiesTests
{
    [Fact]
    public void BuildScoreLookup_prefers_selected_term_and_limits_legacy_entries_to_first_term()
    {
        var assessments = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Score = 8 },
            new() { StudentId = "s1", CriterionId = "c2", Score = 6 },
            new() { StudentId = "s1", CriterionId = "c3", Score = 9 }
        };

        var terms = new Dictionary<string, int?>
        {
            ["c1"] = 1,
            ["c2"] = null,
            ["c3"] = 2
        };

        var firstTerm = AssessmentUtilities.BuildScoreLookup(assessments, terms, 1);
        var secondTerm = AssessmentUtilities.BuildScoreLookup(assessments, terms, 2);
        var thirdTerm = AssessmentUtilities.BuildScoreLookup(assessments, terms, 3);

        Assert.Equal(8, firstTerm[("s1", "c1")]);
        Assert.Equal(6, firstTerm[("s1", "c2")]);
        Assert.False(firstTerm.ContainsKey(("s1", "c3")));

        Assert.Equal(9, secondTerm[("s1", "c3")]);
        Assert.False(secondTerm.ContainsKey(("s1", "c1")));
        Assert.False(secondTerm.ContainsKey(("s1", "c2")));

        Assert.Empty(thirdTerm);
    }

    [Fact]
    public void MergeAssessments_preserves_other_terms_and_replaces_selected_term()
    {
        var existing = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Score = 7 },
            new() { StudentId = "s1", CriterionId = "c1-term-2", Score = 9 },
            new() { StudentId = "s2", CriterionId = "c2", Score = 5 }
        };

        var updates = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Score = 8 }
        };

        var terms = new Dictionary<string, int?>
        {
            ["c1"] = 1,
            ["c1-term-2"] = 2,
            ["c2"] = null
        };

        var merged = AssessmentUtilities.MergeAssessments(existing, updates, terms, 1);

        Assert.Contains(merged, a => a.StudentId == "s1" && a.CriterionId == "c1" && a.Score == 8);
        Assert.Contains(merged, a => a.StudentId == "s1" && a.CriterionId == "c1-term-2" && a.Score == 9);
        Assert.Contains(merged, a => a.StudentId == "s2" && a.CriterionId == "c2" && a.Score == 5);
        Assert.DoesNotContain(merged, a => a.StudentId == "s1" && a.CriterionId == "c1" && a.Score == 7);
    }

    [Fact]
    public void MergeAssessments_drops_legacy_entries_when_replaced_by_updates()
    {
        var existing = new List<Assessment>
        {
            new() { StudentId = "s2", CriterionId = "c2", Score = 4 }
        };

        var updates = new List<Assessment>
        {
            new() { StudentId = "s2", CriterionId = "c2", Score = 6 }
        };

        var terms = new Dictionary<string, int?>
        {
            ["c2"] = null
        };

        var merged = AssessmentUtilities.MergeAssessments(existing, updates, terms, 1);

        Assert.Single(merged, a => a.StudentId == "s2" && a.CriterionId == "c2");
        Assert.Contains(merged, a => a.StudentId == "s2" && a.CriterionId == "c2" && a.Score == 6);
    }
}
