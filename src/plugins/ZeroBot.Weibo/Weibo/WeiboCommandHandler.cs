using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Weibo.Weibo;

public class WeiboCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<WeiboOptions> config,
    WeiboApi api) : CommandHandler(dispatcher)
{
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return text.StartsWith("/微博")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "weibo.subscribe",
                   cancellationToken);
    }

    private Func<string, string, CancellationToken, Task> HandleCommandAsync(Event<IncomingMessage> message)
    {
        return async (op, uid, cancellationToken) =>
        {
            var groupId = message.Data.PeerId;
            await config.BeginConfigMutationScopeAsync(async (value, token) =>
            {
                switch (op)
                {
                    case "订阅":
                        var success = await api.SubscribeAsync(uid, token);
                        if (!success)
                        {
                            await message.ReplyAsGroup(bot, token,
                                [$"订阅用户{uid}的微博失败，请稍后重试！".ToMilkyTextSegment()]);
                            return;
                        }
                        if (!value.UidToGroupSubscriptions.TryGetValue(uid, out var subscriptions))
                            value.UidToGroupSubscriptions.Add(uid, subscriptions = []);
                        subscriptions.Add(groupId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已订阅用户{uid}的微博，更新时将会发送微博通知！".ToMilkyTextSegment()]);
                        break;
                    case "取消":
                        if (value.UidToGroupSubscriptions.TryGetValue(uid, out subscriptions))
                        {
                            subscriptions.Remove(groupId);
                            if (subscriptions.Count == 0)
                            {
                                value.UidToGroupSubscriptions.Remove(uid);
                                value.LastWeiboIds.Remove(uid);
                            }
                        }
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已取消订阅用户{uid}的微博通知！".ToMilkyTextSegment()]);
                        break;
                    default:
                        await message.ReplyAsGroup(bot, token, [HelpStrings]);
                        break;
                }

            }, cancellationToken);
        };
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/微博:订阅:微博用户UID\n" +
         "/微博:取消:微博用户UID").ToMilkyTextSegment();

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var command = message.ToTextCommands().First();
        if (command.Arguments.Length != 2)
        {
            await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
            return;
        }

        await command.InvokeCommandAsync(HandleCommandAsync(message), cancellationToken);
    }
}