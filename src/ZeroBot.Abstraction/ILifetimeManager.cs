namespace ZeroBot.Abstraction;

public interface ILifetimeManager
{
    CancellationToken CancellationToken { get; }
    void Exit();
}
