namespace ZeroBot.Utility.FileWatcher;

public interface IJsonConfig<T>
{
    IAsyncEnumerable<T> WatchChangesAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAsync(T value, CancellationToken cancellationToken = default);
    T Current { get; }

    ValueTask BeginConfigMutationScopeAsync(Func<T, CancellationToken, ValueTask> op,
        CancellationToken cancellationToken = default);
    
    ValueTask<R> BeginConfigMutationScopeAsync<R>(Func<T, CancellationToken, ValueTask<R>> op,
        CancellationToken cancellationToken = default);
    
    ValueTask WaitForInitializedAsync(CancellationToken cancellationToken = default);
}
