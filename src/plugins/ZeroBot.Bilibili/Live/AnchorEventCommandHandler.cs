using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class AnchorEventCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<BilibiliOptions> config,
    AnchorEventApi api) : CommandHandler(dispatcher)
{
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return text.StartsWith("/主播直播间事件")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "bilibili-anchor-event.subscribe",
                   cancellationToken);
    }

    private Func<string, string, CancellationToken, Task> HandleCommandAsync(Event<IncomingMessage> message)
    {
        return async (op, mid, cancellationToken) =>
        {
            var groupId = message.Data.PeerId;
            switch (op)
            {
                case "订阅":
                    var userInfo = await api.GetUserAsync(mid, cancellationToken);
                    if (userInfo == null)
                    {
                        await message.ReplyAsGroup(bot, cancellationToken,
                            ["未找到该主播，请确认 mid 是否正确".ToMilkyTextSegment()]);
                        return;
                    }
                    await config.BeginConfigMutationScopeAsync(async (value, token) =>
                    {
                        if (!value.AnchorEventSubscriptions.TryGetValue(mid, out var subscription))
                        {
                            subscription = new AnchorEventSubscription(
                                userInfo.RoomId.ToString(),
                                userInfo.UName,
                                []);
                            value.AnchorEventSubscriptions.Add(mid, subscription);
                        }
                        subscription.GroupIds.Add(groupId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已订阅主播 {userInfo.UName}({mid}) 的直播间事件，主播发弹幕/入场时将转发到本群！".ToMilkyTextSegment()]);
                    }, cancellationToken);
                    break;
                case "取消":
                    await config.BeginConfigMutationScopeAsync(async (value, token) =>
                    {
                        if (value.AnchorEventSubscriptions.TryGetValue(mid, out var subscription))
                        {
                            subscription.GroupIds.Remove(groupId);
                            if (subscription.GroupIds.Count == 0)
                                value.AnchorEventSubscriptions.Remove(mid);
                            await config.SaveAsync(value, token);
                        }
                        await message.ReplyAsGroup(bot, token,
                            [$"已取消订阅主播 {mid} 的直播间事件转发！".ToMilkyTextSegment()]);
                    }, cancellationToken);
                    break;
                default:
                    await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                    break;
            }
        };
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/主播直播间事件:订阅:B站用户UID\n" +
         "/主播直播间事件:取消:B站用户UID").ToMilkyTextSegment();

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
