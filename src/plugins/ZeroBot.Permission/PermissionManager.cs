using System.Text.Json;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZeroBot.Abstraction.Service;
using ZeroBot.Permission.Abstraction;

namespace ZeroBot.Permission;

public class PermissionManager(
    IOptions<PermissionOption> options,
    IServiceManager serviceManager,
    ILogger<PermissionManager> logger)
    : IPermissionManager, IComponentInitializer
{
    private FileSystemWatcher? _watcher;
    private Dictionary<string, HashSet<string>> _permissions = new();
    private Registration? _serviceRegistration;


    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializePermissionAsync(cancellationToken);
        _serviceRegistration = serviceManager.Register<IPermissionManager>(this);
        var config = options.Value;
        if (config.WatchFileChanges)
        {
            var filePath = Path.GetFullPath(config.PersistFilePath);
            
            var watcher = new FileSystemWatcher();
            _watcher = watcher;
            watcher.Changed += WatcherOnChanged;
            logger.LogInformation("Watching {FilePath} for future permission changes...", filePath);
        }
        logger.LogInformation("Permission manager initialized.");
    }

    private async Task InitializePermissionAsync(CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var filePath = Path.GetFullPath(config.PersistFilePath);
        if (!File.Exists(filePath)) await File.WriteAllTextAsync(filePath, "{}", cancellationToken);
        await using var stream = File.OpenRead(config.PersistFilePath);
        _permissions =
            await JsonSerializer.DeserializeAsync<Dictionary<string, HashSet<string>>>(stream,
                cancellationToken: cancellationToken) ?? [];
    }

    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenWrite(options.Value.PersistFilePath);
        await JsonSerializer.SerializeAsync(stream, _permissions, cancellationToken: cancellationToken);
    }

    private async void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await InitializeAsync();
        }
        catch
        {
            // ignored
        }
    }

    public ValueTask<bool> CheckPermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_permissions.ContainsKey(permission) && _permissions[permission].Contains(principal));
    }

    public async ValueTask<bool> GrantPermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        if (!_permissions.ContainsKey(permission)) _permissions.Add(permission, []);
        _permissions[permission].Add(principal);
        
        await SaveAsync(cancellationToken);
        return true;
    }

    public async ValueTask<bool> RevokePermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        if (!_permissions.TryGetValue(permission, out var permissionSet)) return true;
        if (!permissionSet.Contains(principal)) return true;
        
        permissionSet.Remove(principal);
        await SaveAsync(cancellationToken);
        return true;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public ValueTask DisposeAsync()
    {
        _serviceRegistration?.Dispose();
        _watcher?.Changed -= WatcherOnChanged;
        _watcher?.Dispose();
        return ValueTask.CompletedTask;
    }
}
