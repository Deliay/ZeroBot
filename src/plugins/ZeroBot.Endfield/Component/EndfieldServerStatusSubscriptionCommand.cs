using System.Text.Json;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Component;

public class EndfieldServerStatusSubscriptionCommand(
    IJsonConfig<EndfieldServerStatusSubscriptionConfig> config,
    HttpClient httpClient,
    IBotContext bot)
{
    private const string NetworkConfigEndpoint = "https://endfield-assets.fffdan.com/game/network-config";

    public async ValueTask GetCurrentStatusAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var json = await httpClient.GetStringAsync(NetworkConfigEndpoint, cancellationToken);
        var networkConfig = JsonSerializer.Deserialize<NetworkConfig>(json);
        if (networkConfig is null)
        {
            await message.Reply(bot, cancellationToken,
                ["无法获取服务器状态".ToMilkyTextSegment()]);
            return;
        }

        var isOnline = !networkConfig.GameClose;
        var statusText = isOnline ? "运行中" : "维护中";
        var cached = config.Current.LastKnownStatus;
        var suffix = cached is not null && cached != isOnline
            ? $"\n上次状态: {(cached.Value ? "运行中" : "维护中")}"
            : "";

        await message.Reply(bot, cancellationToken,
            [$"终末地服务器状态: {statusText}{suffix}".ToMilkyTextSegment()]);
    }

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
                    ["已取消订阅终末地服务器状态通知".ToMilkyTextSegment()]);
            }
            else
            {
                value.SubscribedGroupIds.Add(groupId);
                await config.SaveAsync(value, token);
                await message.ReplyAsGroup(bot, token,
                    ["已订阅终末地服务器状态通知，服务器状态变更时将自动通知本群".ToMilkyTextSegment()]);
            }
        }, cancellationToken);
    }

    private record NetworkConfig(bool GameClose);
}