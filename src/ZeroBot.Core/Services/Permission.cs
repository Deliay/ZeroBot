using System.Text.Json;
using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Core.Services;

public class Permission(
    IOptions<PermissionOption> options,
    IServiceManager serviceManager,
    ILogger<Permission> logger)
    : IPermission, IInfrastructureInitializer
{
    private JsonConfig<Permissions> _config = null!;
    private Permissions Permissions => _config.Current!;
    private Registration? _serviceRegistration;


    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        logger.LogInformation("Watching {FilePath} for future permission changes...", config.PersistFilePath);
        _config = new JsonConfig<Permissions>(config.PersistFilePath, [], cancellationToken);
        await _config.InitializeAsync(cancellationToken);
        serviceManager.TryRegister<IPermission>(this, out _serviceRegistration);
        logger.LogInformation("Permission manager initialized.");
    }

    public ValueTask<bool> CheckPermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Permissions.Has(principal, permission));
    }

    public async ValueTask<bool> GrantPermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        Permissions.Grant(principal, permission);
        await _config.SaveAsync(Permissions, cancellationToken);
        return true;
    }

    public async ValueTask<bool> RevokePermissionAsync(string principal, string permission, CancellationToken cancellationToken = default)
    {
        Permissions.Revoke(permission, principal);
        await _config.SaveAsync(Permissions, cancellationToken);
        return true;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public ValueTask DisposeAsync()
    {
        _serviceRegistration?.Dispose();
        _config?.Dispose();
        return ValueTask.CompletedTask;
    }
}
