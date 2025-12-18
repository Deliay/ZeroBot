namespace ZeroBot.Permission.Abstraction;

public interface IPermissionManager
{
    ValueTask<bool> CheckPermissionAsync(string principal, string permission,
        CancellationToken cancellationToken = default);

    ValueTask<bool> GrantPermissionAsync(string principal, string permission,
        CancellationToken cancellationToken = default);

    ValueTask<bool> RevokePermissionAsync(string principal, string permission,
        CancellationToken cancellationToken = default);
}
