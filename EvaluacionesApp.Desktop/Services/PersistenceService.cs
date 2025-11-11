using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Persistence;

namespace EvaluacionesApp.Desktop.Services;

public class PersistenceService
{
    public string DataPath { get; }
    static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public SanityReport LastSanityReport { get; private set; } = SanityReport.Empty;

    public PersistenceService(string? dataPath = null)
    {
        if (dataPath != null)
        {
            DataPath = dataPath;
            return;
        }

        DataPath = GetDefaultDataPath();
    }

    static string GetDefaultDataPath()
    {
        // Get the application data folder based on OS
        // Linux: ~/.local/share/ProyectoAna/
        // Windows: %APPDATA%\ProyectoAna\
        // macOS: ~/Library/Application Support/ProyectoAna/
        
        string appDataFolder;
        
        if (OperatingSystem.IsWindows())
        {
            // Windows: Use APPDATA (Roaming)
            appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux: Use XDG_DATA_HOME or fallback to ~/.local/share
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            appDataFolder = !string.IsNullOrWhiteSpace(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: Use ~/Library/Application Support
            appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else
        {
            // Fallback for other platforms
            appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        
        var appFolder = Path.Combine(appDataFolder, "ProyectoAna");
        return Path.Combine(appFolder, "persistencia.json");
    }

    public async Task<Root> Load()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(DataPath))
            {
                return new Root();
            }
            await using var s = File.OpenRead(DataPath);
            var persisted = await JsonSerializer.DeserializeAsync<PersistedRoot>(s, options);
            var result = ConvertToDomain(persisted);
            LastSanityReport = SanityChecker.Analyze(result);
            SanityChecker.LogReport(LastSanityReport);
            EnsureCourseTerms(result);
            return result;
        }
        catch
        {
            return new Root();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task Save(Root root)
    {
        await _fileLock.WaitAsync();
        try
        {
            // Clean duplicates before saving
            CleanDuplicateAssessments(root);
            
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            
            // Write to temp file first to avoid corruption
            var tempPath = DataPath + ".tmp";
            await using (var s = File.Create(tempPath))
            {
                var persisted = ConvertToPersisted(root);
                await JsonSerializer.SerializeAsync(s, persisted, options);
                await s.FlushAsync();
            }
            
            // Atomic replace
            if (File.Exists(DataPath))
            {
                File.Replace(tempPath, DataPath, null);
            }
            else
            {
                File.Move(tempPath, DataPath);
            }
        }
        catch
        {
            // Cleanup temp file on error
            var tempPath = DataPath + ".tmp";
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    private static void CleanDuplicateAssessments(Root root)
    {
        foreach (var course in root.Courses)
        {
            foreach (var cls in course.Classes)
            {
                // Only clean if there are duplicates
                var originalCount = cls.Assessments.Count;
                var uniqueKeys = cls.Assessments
                    .Select(a => (a.StudentId, a.CriterionId))
                    .Distinct()
                    .Count();

                if (originalCount > uniqueKeys)
                {
                    // Group assessments by (studentId, criterionId) and take the last one
                    cls.Assessments = cls.Assessments
                        .GroupBy(a => (a.StudentId, a.CriterionId))
                        .Select(g => g.Last())
                        .ToList();
                }
            }
        }
    }

    static Root ConvertToDomain(PersistedRoot? persisted)
    {
        if (persisted == null)
        {
            return new Root();
        }

        var root = new Root
        {
            Version = string.IsNullOrWhiteSpace(persisted.Version) ? "1.0" : persisted.Version,
            Courses = persisted.Courses?.Select(ConvertCourseToDomain).ToList() ?? new List<Course>()
        };

        return root;
    }

    static Course ConvertCourseToDomain(PersistedCourse source)
    {
        var course = new Course
        {
            Id = source.Id ?? string.Empty,
            Name = source.Name ?? string.Empty,
            Number = source.Number,
            Terms = source.Terms?.Distinct().OrderBy(x => x).ToList() ?? new List<int>(),
            Classes = source.Classes?.Select(ConvertClassToDomain).ToList() ?? new List<Class>(),
            Criteria = new List<Criterion>()
        };

        if (source.Classes != null)
        {
            foreach (var cls in source.Classes)
            {
                var criteria = ConvertAssessmentsToCriteria(cls).ToList();
                if (criteria.Count == 0)
                {
                    continue;
                }

                course.Criteria = criteria;
                break;
            }
        }

        return course;
    }

    static Class ConvertClassToDomain(PersistedClass source)
    {
        return new Class
        {
            Id = source.Id ?? string.Empty,
            Name = source.Name ?? string.Empty,
            Students = source.Students?.Select(ConvertStudentToDomain).ToList() ?? new List<Student>(),
            Assessments = source.Scores?.Select(ConvertScoreToDomain).ToList() ?? new List<Assessment>()
        };
    }

    static Student ConvertStudentToDomain(PersistedStudent source)
    {
        return new Student
        {
            Id = source.Id ?? Guid.NewGuid().ToString(),
            FirstName = source.FirstName ?? string.Empty,
            LastName = source.LastName ?? string.Empty,
            Positivos = Math.Max(0, source.Positivos),
            Negativos = Math.Max(0, source.Negativos),
            Observaciones = source.Observaciones ?? string.Empty
        };
    }

    static Assessment ConvertScoreToDomain(PersistedScore source)
    {
        return new Assessment
        {
            StudentId = source.StudentId ?? string.Empty,
            CriterionId = source.AssessmentId ?? string.Empty,
            Score = source.Value
        };
    }

    static IEnumerable<Criterion> ConvertAssessmentsToCriteria(PersistedClass source)
    {
        if (source.Assessments == null || source.Assessments.Count == 0)
        {
            return Array.Empty<Criterion>();
        }

        var nodes = new Dictionary<string, Criterion>(StringComparer.Ordinal);

        foreach (var assessment in source.Assessments)
        {
            if (string.IsNullOrWhiteSpace(assessment.Id))
            {
                continue;
            }

            var criterion = new Criterion
            {
                Id = assessment.Id,
                Name = assessment.Name ?? string.Empty,
                Weight = assessment.Weight,
                ClassId = string.Empty,
                Term = assessment.Term,
                Children = new List<Criterion>()
            };

            nodes[assessment.Id] = criterion;
        }

        foreach (var assessment in source.Assessments)
        {
            if (string.IsNullOrWhiteSpace(assessment.Id))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assessment.ParentId)
                && nodes.TryGetValue(assessment.ParentId, out var parent)
                && nodes.TryGetValue(assessment.Id, out var child))
            {
                parent.Children.Add(child);
            }
        }

        var roots = new List<Criterion>();
        foreach (var assessment in source.Assessments)
        {
            if (string.IsNullOrWhiteSpace(assessment.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(assessment.ParentId) || !nodes.ContainsKey(assessment.ParentId))
            {
                roots.Add(nodes[assessment.Id]);
            }
        }

        return roots;
    }

    static PersistedRoot ConvertToPersisted(Root root)
    {
        return new PersistedRoot
        {
            Version = string.IsNullOrWhiteSpace(root.Version) ? "1.0" : root.Version,
            Courses = root.Courses.Select(ConvertCourseToPersisted).ToList()
        };
    }

    static PersistedCourse ConvertCourseToPersisted(Course course)
    {
        var flattenedCriteria = FlattenCriteria(course);
        var globalCriteria = flattenedCriteria
            .Where(entry => string.IsNullOrWhiteSpace(entry.ClassId))
            .ToList();

        var criteriaByClass = flattenedCriteria
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ClassId))
            .GroupBy(entry => entry.ClassId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var classes = new List<PersistedClass>();

        foreach (var cls in course.Classes)
        {
            var entries = new List<CriterionEntry>();

            if (globalCriteria.Count > 0)
            {
                entries.AddRange(globalCriteria.Select(entry => entry.WithClass(cls.Id)));
            }

            if (criteriaByClass.TryGetValue(cls.Id, out var list))
            {
                entries.AddRange(list);
            }

            classes.Add(ConvertClassToPersisted(cls, entries));
        }

        // Preserve orphaned criteria by creating lightweight classes so information is not lost.
        foreach (var group in criteriaByClass)
        {
            if (course.Classes.Any(c => string.Equals(c.Id, group.Key, StringComparison.Ordinal)))
            {
                continue;
            }

            classes.Add(new PersistedClass
            {
                Id = group.Key,
                Name = group.Key,
                Students = new List<PersistedStudent>(),
                Assessments = group.Value.Select(CreatePersistedAssessment).ToList(),
                Scores = new List<PersistedScore>()
            });
        }

        if (classes.Count == 0 && globalCriteria.Count > 0)
        {
            var defaultClassId = string.IsNullOrWhiteSpace(course.Id) ? Guid.NewGuid().ToString() : course.Id!;
            var defaultClassName = string.IsNullOrWhiteSpace(course.Name) ? defaultClassId : course.Name!;
            classes.Add(new PersistedClass
            {
                Id = defaultClassId,
                Name = defaultClassName,
                Students = new List<PersistedStudent>(),
                Assessments = globalCriteria
                    .Select(entry => CreatePersistedAssessment(entry.WithClass(defaultClassId)))
                    .ToList(),
                Scores = new List<PersistedScore>()
            });
        }

        return new PersistedCourse
        {
            Id = course.Id ?? string.Empty,
            Name = course.Name ?? string.Empty,
            Number = course.Number,
            Terms = course.Terms?.Distinct().OrderBy(x => x).ToList() ?? new List<int>(),
            Classes = classes
        };
    }

    static PersistedClass ConvertClassToPersisted(Class cls, List<CriterionEntry> criteria)
    {
        return new PersistedClass
        {
            Id = cls.Id ?? string.Empty,
            Name = cls.Name ?? string.Empty,
            Students = cls.Students?.Select(ConvertStudentToPersisted).ToList() ?? new List<PersistedStudent>(),
            Assessments = criteria.Select(CreatePersistedAssessment).ToList(),
            Scores = cls.Assessments?.Select(ConvertScoreToPersisted).ToList() ?? new List<PersistedScore>()
        };
    }

    static PersistedAssessment CreatePersistedAssessment(CriterionEntry entry)
    {
        var criterion = entry.Criterion;
        return new PersistedAssessment
        {
            Id = criterion.Id,
            Name = criterion.Name,
            Term = criterion.Term,
            Weight = decimal.Clamp(criterion.Weight, 0m, 1m),
            ParentId = entry.ParentId
        };
    }

    static PersistedStudent ConvertStudentToPersisted(Student student)
    {
        return new PersistedStudent
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Positivos = Math.Max(0, student.Positivos),
            Negativos = Math.Max(0, student.Negativos),
            Observaciones = student.Observaciones ?? string.Empty
        };
    }

    static PersistedScore ConvertScoreToPersisted(Assessment assessment)
    {
        return new PersistedScore
        {
            StudentId = assessment.StudentId,
            AssessmentId = assessment.CriterionId,
            Value = assessment.Score
        };
    }

    static List<CriterionEntry> FlattenCriteria(Course course)
    {
        var result = new List<CriterionEntry>();

        void Walk(Criterion criterion, string? parentId, string? inheritedClassId)
        {
            var effectiveClassId = string.IsNullOrWhiteSpace(criterion.ClassId)
                ? inheritedClassId
                : criterion.ClassId;

            result.Add(new CriterionEntry(effectiveClassId, parentId, criterion));

            foreach (var child in criterion.Children)
            {
                Walk(child, criterion.Id, effectiveClassId);
            }
        }

        foreach (var root in course.Criteria)
        {
            Walk(root, null, null);
        }

        return result;
    }

    private sealed record CriterionEntry(string? ClassId, string? ParentId, Criterion Criterion)
    {
        public CriterionEntry WithClass(string classId) => this with { ClassId = classId };
    }

    private sealed class PersistedRoot
    {
        public string? Version { get; set; }
        public List<PersistedCourse> Courses { get; set; } = new();
    }

    private sealed class PersistedCourse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Number { get; set; }
        public List<int> Terms { get; set; } = new();
        public List<PersistedClass> Classes { get; set; } = new();
    }

    private sealed class PersistedClass
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<PersistedStudent> Students { get; set; } = new();
        public List<PersistedAssessment> Assessments { get; set; } = new();
        public List<PersistedScore> Scores { get; set; } = new();
    }

    private sealed class PersistedStudent
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Positivos { get; set; }
        public int Negativos { get; set; }
        public string? Observaciones { get; set; }
    }

    private sealed class PersistedAssessment
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Term { get; set; }
        public decimal Weight { get; set; }
        public string? ParentId { get; set; }
    }

    private sealed class PersistedScore
    {
        public string? StudentId { get; set; }
        public string? AssessmentId { get; set; }
        public decimal? Value { get; set; }
    }

    static void EnsureCourseTerms(Root root)
    {
        foreach (var course in root.Courses)
        {
            if (course.Terms.Count == 0)
            {
                course.Terms = DeriveTerms(course.Criteria);
            }
            else
            {
                course.Terms = course.Terms
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }
        }
    }

    static List<int> DeriveTerms(IEnumerable<Criterion> criteria)
    {
        var set = new HashSet<int>();

        void Walk(IEnumerable<Criterion> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Term.HasValue)
                {
                    set.Add(node.Term.Value);
                }

                if (node.Children.Count > 0)
                {
                    Walk(node.Children);
                }
            }
        }

        Walk(criteria);
        set.Add(1);
        return set.Count > 0 ? set.OrderBy(x => x).ToList() : new List<int> { 1, 2, 3 };
    }
}
