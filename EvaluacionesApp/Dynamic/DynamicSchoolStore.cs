using System;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Dynamic;

public class DynamicSchoolStore : IDisposable
{
    private readonly PersistenceService persistenceService;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DynamicRoot? root;
    private bool disposed;

    public DynamicSchoolStore(PersistenceService persistenceService)
    {
        this.persistenceService = persistenceService;
    }

    public async Task<DynamicRoot> GetRoot(CancellationToken cancellationToken = default)
    {
        if (root != null)
        {
            return root;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (root == null)
            {
                var model = await persistenceService.Load().ConfigureAwait(false);
                root = new DynamicRoot(model);
            }
        }
        finally
        {
            gate.Release();
        }

        return root!;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var currentRoot = await GetRoot(cancellationToken).ConfigureAwait(false);
        var model = currentRoot.ToDomain();
        await persistenceService.Save(model).ConfigureAwait(false);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            root?.Dispose();
            var model = await persistenceService.Load().ConfigureAwait(false);
            root = new DynamicRoot(model);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        root?.Dispose();
        gate.Dispose();
    }
}
