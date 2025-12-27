using Mikibot.Crawler.Http.Bilibili;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class LiveStatutCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<BilibiliOptions> config,
    BiliLiveCrawler crawler) : CommandHandler(dispatcher)
{
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText();
        return text.StartsWith("/直播状态")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "live-stastus.subscribe",
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
                        if (!value.RoomIdToGroupSubscriptions.TryGetValue(roomId, out var subscriptions))
                            value.RoomIdToGroupSubscriptions.Add(roomId, subscriptions = []);
                        var info = await crawler.GetRealRoomInfo(long.Parse(roomId), token);
                        subscriptions.Add(groupId);
                        // try initializing live status to false 
                        value.LastLiveStatus.TryAdd(roomId, false);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已订阅直播间{roomId}(用户{info.BId})的直播，开播时将会发送开播通知！".ToMilkyTextSegment()]);
                        break;
                    case "取消":
                        if (!value.RoomIdToGroupSubscriptions.TryGetValue(roomId, out subscriptions)) break;
                        subscriptions.Remove(groupId);
                        await config.SaveAsync(value, token);
                        await message.ReplyAsGroup(bot, token,
                            [$"已取消订阅直播间{roomId}的开播通知！".ToMilkyTextSegment()]);
                        break;
                    default:
                        await message.ReplyAsGroup(bot, token, [HelpStrings]);
                        break;
                }
                
            }, cancellationToken);
        };
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/直播状态:订阅:B站直播间ID\n" +
         "/直播状态:取消:B站直播间ID").ToMilkyTextSegment();
    
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