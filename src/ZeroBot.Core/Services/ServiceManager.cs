using System.Diagnostics.CodeAnalysis;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Core.Services;

public class ServiceManager : IServiceManager
{
    private readonly Dictionary<Type, object> _services = [];
    private readonly Dictionary<Type, List<TaskCompletionSource>> _waiters = [];
    
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

    public bool TryRegister<T>(T service, [NotNullWhen(true)]out Registration? registration) where T : class
    {
        _semaphore.Wait();
        try
        {
            if (_services.TryGetValue(typeof(T), out var exists))
            {
                registration = null;
                return false;
            }
            else
            {
                _services.Add(typeof(T), service);

                registration = new Registration(() => _services.Remove(typeof(T)));

                if (_waiters.TryGetValue(typeof(T), out var waiters))
                {
                    foreach (var tcs in waiters)
                    {
                        tcs.TrySetResult();
                    }
                }
                return true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private readonly SemaphoreSlim _semaphore = new(1);
    
    public async ValueTask<T> WaitServiceAsync<T>(CancellationToken cancellationToken) where T : class
    {
        if (TryResolve<T>(out var service)) return service;
        
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_waiters.TryGetValue(typeof(T), out var waiters)) _waiters.Add(typeof(T), waiters = []);
            var tcs = new TaskCompletionSource();
            waiters.Add(tcs);
            await tcs.Task;
            return Resolve<T>();
        }
        finally
        {
            _semaphore.Release();
        }
        
    }
}
