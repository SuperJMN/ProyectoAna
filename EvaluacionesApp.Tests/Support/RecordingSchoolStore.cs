using System;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Dynamic;
using EvaluacionesApp.Models;

namespace EvaluacionesApp.Tests.Support;

public sealed class RecordingSchoolStore : IDynamicSchoolStore
{
    private readonly DynamicRoot root;

    public RecordingSchoolStore(DynamicRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        this.root = root;
    }

    public int SaveCount { get; private set; }

    public int ReloadCount { get; private set; }

    public static RecordingSchoolStore FromDomain(Root domainRoot)
    {
        ArgumentNullException.ThrowIfNull(domainRoot);
        return new RecordingSchoolStore(new DynamicRoot(domainRoot));
    }

    public Task<DynamicRoot> GetRoot(CancellationToken cancellationToken = default)
        => Task.FromResult(root);

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ReloadCount++;
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        root.Dispose();
    }
}
