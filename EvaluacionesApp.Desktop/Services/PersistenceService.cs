using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Models;

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

    public PersistenceService(string? dataPath = null)
    {
        if (dataPath != null)
        {
            DataPath = dataPath;
            return;
        }

        // Try to resolve persistencia.json by walking up the directory tree, avoiding build folders (bin/obj)
        DataPath = ResolveDataPath() ?? Path.Combine(Directory.GetCurrentDirectory(), "persistencia.json");
    }

    static string? ResolveDataPath()
    {
        // Prefer a file outside build output folders. Search from both CWD and BaseDirectory upwards.
        var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts)
        {
            var path = FindUpwards(start);
            if (path != null) return path;
        }
        return null;
    }

    static string? FindUpwards(string startDir)
    {
        var di = new DirectoryInfo(startDir);
        string? best = null;
        while (di != null)
        {
            var candidate = Path.Combine(di.FullName, "persistencia.json");
            if (File.Exists(candidate) && !IsInBuildFolder(di.FullName))
            {
                // Keep walking to prefer higher-level files (e.g., repo root) over bin copies
                best = candidate;
            }
            di = di.Parent;
        }
        return best;
    }

    static bool IsInBuildFolder(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => string.Equals(p, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "obj", StringComparison.OrdinalIgnoreCase));
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
            var root = await JsonSerializer.DeserializeAsync<Root>(s, options);
            return root ?? new Root();
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
                await JsonSerializer.SerializeAsync(s, root, options);
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
                    .Select(a => (a.StudentId, a.CriterionId, a.Term))
                    .Distinct()
                    .Count();
                    
                if (originalCount > uniqueKeys)
                {
                    // Group assessments by (studentId, criterionId, term) and take the last one
                    cls.Assessments = cls.Assessments
                        .GroupBy(a => (a.StudentId, a.CriterionId, a.Term))
                        .Select(g => g.Last())
                        .ToList();
                }
            }
        }
    }
}
