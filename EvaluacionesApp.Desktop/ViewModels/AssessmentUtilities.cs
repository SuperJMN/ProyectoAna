using System;
using System.Collections.Generic;
using System.Linq;
using EvaluacionesApp.Desktop.Models;

namespace EvaluacionesApp.Desktop.ViewModels;

internal static class AssessmentUtilities
{
    public static Dictionary<(string studentId, string criterionId), decimal?> BuildScoreLookup(
        IEnumerable<Assessment> assessments,
        IReadOnlyDictionary<string, int?> criterionTerms,
        int selectedTerm)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(criterionTerms);

        var map = new Dictionary<(string studentId, string criterionId), decimal?>();

        foreach (var assessment in assessments)
        {
            if (string.IsNullOrWhiteSpace(assessment.StudentId) || string.IsNullOrWhiteSpace(assessment.CriterionId))
            {
                continue;
            }

            var key = (assessment.StudentId, assessment.CriterionId);
            var term = ResolveTerm(criterionTerms, assessment.CriterionId);

            if (term.HasValue)
            {
                if (term.Value == selectedTerm)
                {
                    map[key] = assessment.Score;
                }
                continue;
            }

            if (selectedTerm == 1 && !map.ContainsKey(key))
            {
                map[key] = assessment.Score;
            }
        }

        return map;
    }

    public static List<Assessment> MergeAssessments(
        IEnumerable<Assessment> existing,
        IEnumerable<Assessment> updates,
        IReadOnlyDictionary<string, int?> criterionTerms,
        int selectedTerm)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(criterionTerms);

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
            var term = ResolveTerm(criterionTerms, assessment.CriterionId);

            if (term.HasValue)
            {
                if (term.Value != selectedTerm)
                {
                    preserved.Add(assessment);
                }

                continue;
            }

            if (selectedTerm != 1 || !updatesByKey.ContainsKey(key))
            {
                preserved.Add(assessment);
            }
        }

        preserved.AddRange(updatesByKey.Values);
        return preserved;
    }

    static int? ResolveTerm(IReadOnlyDictionary<string, int?> terms, string criterionId)
    {
        if (string.IsNullOrWhiteSpace(criterionId))
        {
            return null;
        }

        return terms.TryGetValue(criterionId, out var term) ? term : null;
    }
}
