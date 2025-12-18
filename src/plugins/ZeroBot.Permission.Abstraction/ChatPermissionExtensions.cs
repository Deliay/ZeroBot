namespace ZeroBot.Permission.Abstraction;

public static class ChatPermissionExtensions
{
    extension(IPermissionManager manager)
    {
        public ValueTask<bool> CheckUserInGroupPermissionAsync(long group, long user, string permission,
            CancellationToken cancellationToken = default)
        {
            return manager.CheckPermissionAsync($"user-{user}", $"group-{group}-{permission}", cancellationToken);
        }
        
        public ValueTask<bool> GrantUserInGroupPermissionAsync(long group, long user, string permission,
            CancellationToken cancellationToken = default)
        {
            return manager.GrantPermissionAsync($"user-{user}", $"group-{group}-{permission}", cancellationToken);
        }
        
        public ValueTask<bool> RevokeUserInGroupPermissionAsync(long group, long user, string permission,
            CancellationToken cancellationToken = default)
        {
            return manager.RevokePermissionAsync($"user-{user}", $"group-{group}-{permission}", cancellationToken);
        }

        public ValueTask<bool> CheckUserPermissionAsync(long user, string permission,
            CancellationToken cancellationToken = default) =>
            manager.CheckPermissionAsync($"user-{user}", permission, cancellationToken);
        
        public ValueTask<bool> GrantUserPermissionAsync(long user, string permission,
            CancellationToken cancellationToken = default) =>
            manager.GrantPermissionAsync($"user-{user}", permission, cancellationToken);

        public ValueTask<bool> RevokeUserPermissionAsync(long user, string permission,
            CancellationToken cancellationToken) =>
            manager.RevokePermissionAsync($"user-{user}", permission, cancellationToken);

        public ValueTask<bool> CheckGroupPermissionAsync(long group, string permission,
            CancellationToken cancellationToken = default) =>
            manager.CheckPermissionAsync($"group-{group}", permission, cancellationToken);
        
        public ValueTask<bool> GrantGroupPermissionAsync(long group, string permission,
            CancellationToken cancellationToken = default) =>
            manager.GrantPermissionAsync($"group-{group}", permission, cancellationToken);

        public ValueTask<bool> RevokeGroupPermissionAsync(long group, string permission,
            CancellationToken cancellationToken = default) =>
            manager.RevokePermissionAsync($"group-{group}", permission, cancellationToken);
    }
}
