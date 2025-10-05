using System;
using System.Collections.Generic;
using System.Linq;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.ViewModels;

internal static class AssessmentUtilities
{
    public static Dictionary<(string studentId, string criterionId), double?> BuildScoreLookup(
        IEnumerable<Assessment> assessments,
        int selectedTerm)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var map = new Dictionary<(string studentId, string criterionId), double?>();

        foreach (var assessment in assessments)
        {
            if (string.IsNullOrWhiteSpace(assessment.StudentId) || string.IsNullOrWhiteSpace(assessment.CriterionId))
            {
                continue;
            }

            var key = (assessment.StudentId, assessment.CriterionId);

            if (assessment.Term == selectedTerm)
            {
                map[key] = assessment.Score;
                continue;
            }

            if (!assessment.Term.HasValue && selectedTerm == 1 && !map.ContainsKey(key))
            {
                map[key] = assessment.Score;
            }
        }

        return map;
    }

    public static List<Assessment> MergeAssessments(
        IEnumerable<Assessment> existing,
        IEnumerable<Assessment> updates,
        int selectedTerm)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(updates);

        var updateList = updates
            .Where(a => !string.IsNullOrWhiteSpace(a.StudentId) && !string.IsNullOrWhiteSpace(a.CriterionId))
            .ToList();

        var updatesByKey = updateList
            .GroupBy(a => (a.StudentId, a.CriterionId))
            .ToDictionary(group => group.Key, group => group.Last());

        var preserved = new List<Assessment>();

        foreach (var assessment in existing)
        {
            if (string.IsNullOrWhiteSpace(assessment.StudentId) || string.IsNullOrWhiteSpace(assessment.CriterionId))
            {
                continue;
            }

            var key = (assessment.StudentId, assessment.CriterionId);

            if (assessment.Term.HasValue)
            {
                if (assessment.Term.Value != selectedTerm)
                {
                    preserved.Add(assessment);
                }

                continue;
            }

            if (!updatesByKey.ContainsKey(key))
            {
                preserved.Add(assessment);
            }
        }

        preserved.AddRange(updatesByKey.Values);
        return preserved;
    }
}
