using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.UI;

namespace EvaluacionesApp.Tests.Support;

public sealed class NullNotificationService : INotificationService
{
    public Task Show(string message, Maybe<string> title)
    {
        return Task.CompletedTask;
    }
}
