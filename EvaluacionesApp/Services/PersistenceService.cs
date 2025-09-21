using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Services;

public class PersistenceService
{
    public string DataPath { get; }
    static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PersistenceService(string? dataPath = null)
    {
        if (dataPath != null)
        {
            DataPath = dataPath;
            return;
        }
        var cwd = Directory.GetCurrentDirectory();
        var candidate = Path.Combine(cwd, "persistencia.json");
        var parent = Directory.GetParent(cwd)?.FullName;
        var parentCandidate = parent is null ? null : Path.Combine(parent, "persistencia.json");
        if (parentCandidate != null && File.Exists(parentCandidate))
        {
            DataPath = parentCandidate;
        }
        else
        {
            DataPath = candidate;
        }
    }

    public async Task<Root> Load()
    {
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
    }

    public async Task Save(Root root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        await using var s = File.Create(DataPath);
        await JsonSerializer.SerializeAsync(s, root, options);
        await s.FlushAsync();
    }
}