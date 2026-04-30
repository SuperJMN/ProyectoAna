using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;

namespace EvaluacionesApp.Tests.Support;

public sealed class FakeSchoolStore : IDynamicSchoolStore
{
    public FakeSchoolStore(DynamicRoot root)
    {
        Root = root;
    }

    public DynamicRoot Root { get; }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Dispose()
    {
        Root.Dispose();
    }
}
