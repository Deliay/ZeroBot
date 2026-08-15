using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class LiveScCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<BilibiliOptions> config) : CommandHandler(dispatcher)
{
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return text.StartsWith("/直播SC")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "bilibili-sc.subscribe",
                   cancellationToken);
    }

    private Func<string, string, CancellationToken, Task> HandleCommandAsync(Event<IncomingMessage> message)
    {
        return async (op, roomId, cancellationToken) =>
        {
            var groupId = message.Data.PeerId;
            await config.BeginConfigMutationScopeAsync(async (value, token) =>
            {
                switch (op)
                {
                    case "订阅":
                        if (!value.ScRoomIdToGroupSubscriptions.TryGetValue(roomId, out var subscriptions))
                            value.ScRoomIdToGroupSubscriptions.Add(roomId, subscriptions = []);
                        subscriptions.Add(groupId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已订阅直播间{roomId}的醒目留言，有新SC时将会转发到本群！".ToMilkyTextSegment()]);
                        break;
                    case "取消":
                        if (!value.ScRoomIdToGroupSubscriptions.TryGetValue(roomId, out subscriptions)) break;
                        subscriptions.Remove(groupId);
                        if (subscriptions.Count == 0) value.ScRoomIdToGroupSubscriptions.Remove(roomId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已取消订阅直播间{roomId}的醒目留言转发！".ToMilkyTextSegment()]);
                        break;
                    default:
                        await message.ReplyAsGroup(bot, token, [HelpStrings]);
                        break;
                }

            }, cancellationToken);
        };
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/直播SC:订阅:B站直播间ID\n" +
         "/直播SC:取消:B站直播间ID").ToMilkyTextSegment();

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
