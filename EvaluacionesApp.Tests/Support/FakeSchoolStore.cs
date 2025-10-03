using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Dynamic;

namespace EvaluacionesApp.Tests.Support;

public sealed class FakeSchoolStore : IDynamicSchoolStore
{
    private readonly DynamicRoot root;

    public FakeSchoolStore(DynamicRoot root)
    {
        this.root = root;
    }

    public Task<DynamicRoot> GetRoot(CancellationToken cancellationToken = default)
        => Task.FromResult(root);

    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Dispose()
    {
        root.Dispose();
    }
}
