using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Utility;

public static class ChatPermissionExtensions
{
    extension(IPermission manager)
    {
        public ValueTask<bool> IsSudoerAsync(long user, CancellationToken cancellationToken = default)
        {
            return manager.CheckUserPermissionAsync(user, "sudoers",
                cancellationToken);
        }

        public ValueTask<bool> IsFromAdminAsync(IBotContext bot, Event<IncomingMessage> message,
            CancellationToken cancellationToken = default)
        {
            return bot.IsGroupAdminAsync(message.SelfId, message.Data.PeerId, message.Data.SenderId, cancellationToken);
        }
        
        
        public async ValueTask<bool> IsSudoerOrGroupAdminAsync(
            IBotContext bot, Event<IncomingMessage> message,
            CancellationToken cancellationToken = default)
        {
            return await manager.IsSudoerAsync(message.Data.SenderId, cancellationToken)
                   || await manager.IsFromAdminAsync(bot, message, cancellationToken);
        }
        
        public async ValueTask<bool> IsSudoerOrGroupAdminOrHasPermissionAsync(
            IBotContext bot, Event<IncomingMessage> message, string permission,
            CancellationToken cancellationToken = default)
        {
            return await manager.IsSudoerOrGroupAdminAsync(bot, message, cancellationToken)
                   || await manager.CheckUserInGroupPermissionAsync(message.Data.PeerId, message.Data.SenderId,
                       permission, cancellationToken);
        }
        
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
