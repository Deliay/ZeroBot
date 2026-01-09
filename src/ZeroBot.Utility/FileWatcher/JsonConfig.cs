using System.Runtime.CompilerServices;
using System.Text.Json;
using EmberFramework.Abstraction.Layer.Plugin;

namespace ZeroBot.Utility.FileWatcher;

public class JsonConfig<T>(string file, T defaultValue, CancellationToken cancellationToken = default)
    : IComponentInitializer, IJsonConfig<T>
{
    private readonly JsonFileContentWatcher<T> _watcher = new(file, defaultValue, cancellationToken);
    private TaskCompletionSource<T> _tcs = new();

    public void Dispose()
    {
        _watcher.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _watcher.DisposeAsync();
    }

    public async IAsyncEnumerable<T> WatchChangesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await _tcs.Task;
            _tcs = new TaskCompletionSource<T>();
        }
    }

    public async ValueTask SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var tempFile = $"{file}.1";
        await using var stream = File.OpenWrite(tempFile);
        await JsonSerializer.SerializeAsync(stream, value, cancellationToken: cancellationToken);
        File.Replace(tempFile, file, $"{file}.bak");
    }

    private static InvalidOperationException ThrowInvalidConfig() =>
        new("Content not loaded, or provided an invalid default value!");
    
    public T Current => _watcher.Current ?? throw ThrowInvalidConfig();
    
    
    private readonly SemaphoreSlim _semaphore = new(1);
    public async ValueTask BeginConfigMutationScopeAsync(Func<T, CancellationToken, ValueTask> op, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await op(Current, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask<R> BeginConfigMutationScopeAsync<R>(Func<T, CancellationToken, ValueTask<R>> op, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await op(Current, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public ValueTask WaitForInitializedAsync(CancellationToken cancellationToken = default)
    {
        return _watcher.WaitForInitializedAsync(cancellationToken);
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        _watcher.FileChangedAsync += WatcherOnFileChangedAsync;
        return _watcher.InitializeAsync(cancellationToken);
    }

    private ValueTask WatcherOnFileChangedAsync(T? arg1, CancellationToken arg2)
    {
        _tcs.SetResult(arg1 ?? throw ThrowInvalidConfig());
        return default;
    }
}
