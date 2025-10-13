using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using EvaluacionesApp.Desktop.Persistence;

namespace EvaluacionesApp.Desktop.Services;

public static class SanityChecker
{
    public static SanityReport Analyze(Root root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var warnings = new List<string>();

        foreach (var course in root.Courses)
        {
            var criterionIndex = BuildCriterionIndex(course.Criteria);

            foreach (var cls in course.Classes)
            {
                var studentLookup = cls.Students
                    .Where(student => !string.IsNullOrWhiteSpace(student.Id))
                    .ToDictionary(student => student.Id, student => student.FullName, StringComparer.Ordinal);

                var issues = cls.Assessments
                    .Where(assessment => !string.IsNullOrWhiteSpace(assessment.CriterionId) && assessment.Score.HasValue)
                    .Select(assessment => TryResolveCriterion(criterionIndex, assessment, cls.Id))
                    .Where(result => result.HasValue)
                    .Select(result => result.Value)
                    .GroupBy(entry => entry.criterion, entry => entry.assessment.StudentId ?? string.Empty)
                    .ToList();

                foreach (var group in issues)
                {
                    if (group.Key.Children.Count == 0)
                    {
                        continue;
                    }

                    var students = group
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => studentLookup.TryGetValue(id!, out var name) ? name : id!)
                        .Distinct()
                        .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    if (students.Count == 0)
                    {
                        continue;
                    }

                    var message =
                        $"El criterio '{group.Key.Name}' ({group.Key.Id}) de la clase '{cls.Name}' en el curso '{course.Name}' tiene puntuaciones locales para {students.Count} alumno(s): {string.Join(", ", students)}.";
                    warnings.Add(message);
                }
            }
        }

        return warnings.Count == 0
            ? SanityReport.Empty
            : new SanityReport(warnings);
    }

    public static void LogReport(SanityReport report)
    {
        if (report.HasIssues)
        {
            foreach (var warning in report.Warnings)
            {
                Console.Error.WriteLine($"[SanityCheck] {warning}");
            }
        }
    }

    static Maybe<(Criterion criterion, Assessment assessment)> TryResolveCriterion(Dictionary<string, CriterionInfo> index, Assessment assessment, string classId)
    {
        if (string.IsNullOrWhiteSpace(assessment.CriterionId))
        {
            return Maybe<(Criterion, Assessment)>.None;
        }

        if (!index.TryGetValue(assessment.CriterionId, out var info))
        {
            return Maybe<(Criterion, Assessment)>.None;
        }

        if (!info.AppliesToClass(classId))
        {
            return Maybe<(Criterion, Assessment)>.None;
        }

        return Maybe<(Criterion, Assessment)>.From((info.Criterion, assessment));
    }

    static Dictionary<string, CriterionInfo> BuildCriterionIndex(IEnumerable<Criterion> criteria)
    {
        var index = new Dictionary<string, CriterionInfo>(StringComparer.Ordinal);

        void Walk(Criterion criterion, string? currentClassId)
        {
            var effectiveClassId = string.IsNullOrWhiteSpace(criterion.ClassId)
                ? currentClassId
                : criterion.ClassId;

            index[criterion.Id] = new CriterionInfo(criterion, effectiveClassId);

            foreach (var child in criterion.Children)
            {
                Walk(child, effectiveClassId);
            }
        }

        foreach (var criterion in criteria)
        {
            Walk(criterion, null);
        }

        return index;
    }

    readonly record struct CriterionInfo(Criterion Criterion, string? ClassId)
    {
        public bool AppliesToClass(string classId)
        {
            return string.IsNullOrWhiteSpace(ClassId) || string.Equals(ClassId, classId, StringComparison.Ordinal);
        }
    }
}

public sealed record SanityReport(IReadOnlyList<string> Warnings)
{
    public static readonly SanityReport Empty = new(Array.Empty<string>());

    public bool HasIssues => Warnings.Count > 0;
}
