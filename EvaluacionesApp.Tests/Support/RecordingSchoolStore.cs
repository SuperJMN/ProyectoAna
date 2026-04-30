using System;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;

namespace EvaluacionesApp.Tests.Support;

public sealed class RecordingSchoolStore : IDynamicSchoolStore
{
    public RecordingSchoolStore(DynamicRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    public DynamicRoot Root { get; }

    public int SaveCount { get; private set; }

    public static RecordingSchoolStore FromDomain(Root domainRoot)
    {
        ArgumentNullException.ThrowIfNull(domainRoot);
        return new RecordingSchoolStore(new DynamicRoot(domainRoot));
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Root.Dispose();
    }
}
