using System.Diagnostics.CodeAnalysis;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Core.Services;

public class ServiceManager : IServiceManager
{
    private readonly Dictionary<Type, object> _services = [];
    
    public T Resolve<T>() where T : class
    {
        return (T)_services[typeof(T)];
    }

    public bool TryResolve<T>([NotNullWhen(true)] out T? service) where T : class
    {
        var result = _services.TryGetValue(typeof(T), out var obj);
        if (result) service = (T)obj!;
        else service = null;
        return result;
    }

    public Registration Register<T>(T service) where T : class
    {
        _services.Add(typeof(T), service);

        return new Registration(() => _services.Remove(typeof(T)));
    }
}
