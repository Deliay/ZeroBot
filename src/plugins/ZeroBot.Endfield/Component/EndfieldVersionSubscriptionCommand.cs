using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Component;

public class EndfieldVersionSubscriptionCommand(
    IJsonConfig<EndfieldVersionSubscriptionConfig> config,
    IBotContext bot)
{
    public async ValueTask ToggleSubscriptionAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var groupId = message.Data.PeerId;

        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            var subscribed = value.SubscribedGroupIds.Contains(groupId);
            if (subscribed)
            {
                value.SubscribedGroupIds.Remove(groupId);
                await config.SaveAsync(value, token);
                await message.ReplyAsGroup(bot, token,
                    ["已取消订阅终末地版本更新通知".ToMilkyTextSegment()]);
            }
            else
            {
                value.SubscribedGroupIds.Add(groupId);
                await config.SaveAsync(value, token);
                await message.ReplyAsGroup(bot, token,
                    ["已订阅终末地版本更新通知，版本变更时将自动通知本群".ToMilkyTextSegment()]);
            }
        }, cancellationToken);
    }
}
