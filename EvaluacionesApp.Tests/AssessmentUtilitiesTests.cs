using System.Collections.Generic;
using EvaluacionesApp.Models;
using EvaluacionesApp.ViewModels;
using Xunit;

namespace EvaluacionesApp.Tests;

public class AssessmentUtilitiesTests
{
    [Fact]
    public void BuildScoreLookup_prefers_selected_term_and_falls_back_to_legacy_entries()
    {
        var assessments = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Term = 1, Score = 8 },
            new() { StudentId = "s1", CriterionId = "c1", Term = 2, Score = 5 },
            new() { StudentId = "s1", CriterionId = "c1", Term = null, Score = 7 },
            new() { StudentId = "s1", CriterionId = "c2", Term = null, Score = 6 }
        };

        var firstTerm = AssessmentUtilities.BuildScoreLookup(assessments, 1);
        var thirdTerm = AssessmentUtilities.BuildScoreLookup(assessments, 3);

        Assert.Equal(8, firstTerm[("s1", "c1")]);
        Assert.Equal(6, firstTerm[("s1", "c2")]);
        Assert.Equal(7, thirdTerm[("s1", "c1")]);
        Assert.Equal(6, thirdTerm[("s1", "c2")]);
    }

    [Fact]
    public void MergeAssessments_preserves_other_terms_and_replaces_selected_term()
    {
        var existing = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Term = 1, Score = 7 },
            new() { StudentId = "s1", CriterionId = "c1", Term = 2, Score = 9 },
            new() { StudentId = "s2", CriterionId = "c2", Term = null, Score = 5 }
        };

        var updates = new List<Assessment>
        {
            new() { StudentId = "s1", CriterionId = "c1", Term = 1, Score = 8 }
        };

        var merged = AssessmentUtilities.MergeAssessments(existing, updates, 1);

        Assert.Contains(merged, a => a.StudentId == "s1" && a.CriterionId == "c1" && a.Term == 1 && a.Score == 8);
        Assert.Contains(merged, a => a.StudentId == "s1" && a.CriterionId == "c1" && a.Term == 2 && a.Score == 9);
        Assert.Contains(merged, a => a.StudentId == "s2" && a.CriterionId == "c2" && a.Term == null && a.Score == 5);
        Assert.DoesNotContain(merged, a => a.StudentId == "s1" && a.CriterionId == "c1" && a.Term == 1 && a.Score == 7);
    }

    [Fact]
    public void MergeAssessments_drops_legacy_entries_when_replaced_by_updates()
    {
        var existing = new List<Assessment>
        {
            new() { StudentId = "s2", CriterionId = "c2", Term = null, Score = 4 }
        };

        var updates = new List<Assessment>
        {
            new() { StudentId = "s2", CriterionId = "c2", Term = 1, Score = 6 }
        };

        var merged = AssessmentUtilities.MergeAssessments(existing, updates, 1);

        Assert.Single(merged, a => a.StudentId == "s2" && a.CriterionId == "c2");
        Assert.Contains(merged, a => a.StudentId == "s2" && a.CriterionId == "c2" && a.Term == 1 && a.Score == 6);
    }
}
