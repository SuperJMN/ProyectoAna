using System;
using System.Threading;
using System.Threading.Tasks;

namespace EvaluacionesApp.Desktop.Dynamic;

public interface IDynamicSchoolStore : IDisposable
{
    DynamicRoot Root { get; }
    Task SaveAsync(CancellationToken cancellationToken = default);
}
