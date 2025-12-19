using System.Diagnostics.CodeAnalysis;

namespace ZeroBot.Abstraction.Service;

public interface IServiceManager
{
    public T Resolve<T>() where T : class;
    bool TryResolve<T>([NotNullWhen(true)]out T? service) where T : class;
    public bool TryRegister<T>(T service, [NotNullWhen(true)]out Registration? registration) where T : class;
}
