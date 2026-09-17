using System;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Services;

namespace EvaluacionesApp.Desktop.Dynamic;

public class DynamicSchoolStore : IDynamicSchoolStore
{
    private readonly PersistenceService persistenceService;
    private bool disposed;

    public DynamicSchoolStore(DynamicRoot root, PersistenceService persistenceService)
    {
        Root = root;
        this.persistenceService = persistenceService;
    }

    public DynamicRoot Root { get; }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var model = Root.ToDomain();
        await persistenceService.Save(model, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Root.Dispose();
    }
}
