using ZeroBot.Abstraction;

namespace ZeroBot.Core.Services;

public class LifetimeManager : ILifetimeManager
{
    private readonly CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;
    public void Exit()
    {
        _cts.Cancel();
    }
}
