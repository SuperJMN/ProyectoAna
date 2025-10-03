using System;
using System.Threading;
using System.Threading.Tasks;

namespace EvaluacionesApp.Dynamic;

public interface IDynamicSchoolStore : IDisposable
{
    Task<DynamicRoot> GetRoot(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
