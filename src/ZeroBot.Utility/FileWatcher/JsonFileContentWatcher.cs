using System.Text.Json;
using EmberFramework.Abstraction.Layer.Plugin;

namespace ZeroBot.Utility.FileWatcher;

public class JsonFileContentWatcher<T> : IComponentInitializer
{
    private readonly string _path;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly FileSystemWatcher _watcher;
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly TaskCompletionSource _initialCts = new();
    public T Current { get; private set; }

    public JsonFileContentWatcher(string path, T defaultContent, CancellationToken cancellationToken = default)
    {
        _path = Path.GetFullPath(path);
        Current = defaultContent;
        var fileDir = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Invalid file path");
        _watcher = new FileSystemWatcher(fileDir, Path.GetFileName(_path));
        _watcher.Changed += WatcherOnChanged;
        _watcher.EnableRaisingEvents = true;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public async ValueTask WaitForInitializedAsync(CancellationToken cancellationToken = default)
    {
        await _initialCts.Task;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await UpdateCurrentValueAsync(cancellationToken);
            _initialCts.SetResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async ValueTask UpdateCurrentValueAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_path))
        {
            await using var file = File.OpenRead(_path);
            Current = await JsonSerializer.DeserializeAsync<T>(file, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Invalid configuration!");
            return;
        }

        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(Current), cancellationToken);
    }
    
    private void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        _ = ApplyFileChangesAsync(_cancellationTokenSource.Token);
    }
    
    private async Task ApplyFileChangesAsync(CancellationToken cancellationToken)
    {
        if (_semaphore.CurrentCount == 0) return;
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await UpdateCurrentValueAsync(cancellationToken);
            await (FileChangedAsync?.Invoke(Current, cancellationToken) ?? default);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public event Func<T?, CancellationToken, ValueTask>? FileChangedAsync;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _watcher?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_cancellationTokenSource);
        await CastAndDispose(_watcher);
        await CastAndDispose(_semaphore);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}