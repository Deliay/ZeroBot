using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Dynamic;

public class DynamicCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<BilibiliOptions> config,
    VtuberSpaceApi api) : CommandHandler(dispatcher)
{
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return text.StartsWith("/B站动态")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "bilibili-dynamic.subscribe",
                   cancellationToken);
    }

    private Func<string, string, CancellationToken, Task> HandleCommandAsync(Event<IncomingMessage> message)
    {
        return async (op, mid, cancellationToken) =>
        {
            var groupId = message.Data.PeerId;
            await config.BeginConfigMutationScopeAsync(async (value, token) =>
            {
                switch (op)
                {
                    case "订阅":
                        await api.SubscribeAsync(mid, token);
                        if (!value.MidToGroupSubscriptions.TryGetValue(mid, out var subscriptions))
                            value.MidToGroupSubscriptions.Add(mid, subscriptions = []);
                        subscriptions.Add(groupId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已订阅用户{mid}的B站动态，更新时将会发送动态通知！".ToMilkyTextSegment()]);
                        break;
                    case "取消":
                        await api.UnsubscribeAsync(mid, token);
                        if (value.MidToGroupSubscriptions.TryGetValue(mid, out subscriptions))
                        {
                            subscriptions.Remove(groupId);
                            if (subscriptions.Count == 0)
                            {
                                value.MidToGroupSubscriptions.Remove(mid);
                                value.LastDynamicIds.Remove(mid);
                            }
                        }
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已取消订阅用户{mid}的B站动态通知！".ToMilkyTextSegment()]);
                        break;
                    default:
                        await message.ReplyAsGroup(bot, token, [HelpStrings]);
                        break;
                }

            }, cancellationToken);
        };
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/B站动态:订阅:B站用户UID\n" +
         "/B站动态:取消:B站用户UID").ToMilkyTextSegment();

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
