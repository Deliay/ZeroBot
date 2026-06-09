using System.Text.Json;
using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Component;

public class EndfieldServerStatusPollingTask(
    IJsonConfig<EndfieldServerStatusSubscriptionConfig> config,
    HttpClient httpClient,
    ILogger<EndfieldServerStatusPollingTask> logger,
    IBotContext bot) : IExecutable
{
    private const string NetworkConfigEndpoint = "https://endfield-assets.fffdan.com/game/network-config";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Server status polling failed");
            }
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async ValueTask PollAsync(CancellationToken cancellationToken)
    {
        var json = await httpClient.GetStringAsync(NetworkConfigEndpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return;

        var networkConfig = JsonSerializer.Deserialize<NetworkConfig>(json);
        if (networkConfig is null) return;

        var isOnline = !networkConfig.GameClose;

        if (config.Current.LastKnownStatus is null)
        {
            await SaveStatusAsync(isOnline, cancellationToken);
            return;
        }

        if (isOnline == config.Current.LastKnownStatus) return;

        await NotifySubscribersAsync(isOnline, cancellationToken);
        await SaveStatusAsync(isOnline, cancellationToken);
    }

    private async ValueTask NotifySubscribersAsync(bool isOnline, CancellationToken cancellationToken)
    {
        if (config.Current.SubscribedGroupIds.Count == 0) return;

        var statusText = isOnline ? "已开服" : "已维护";
        var message = $"终末地服务器状态变更！当前状态: {statusText}".ToMilkyTextSegment();
        await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
        {
            await bot.WriteManyGroupMessageAsync(
                accountId,
                config.Current.SubscribedGroupIds,
                cancellationToken,
                message);
        }
    }

    private ValueTask SaveStatusAsync(bool isOnline, CancellationToken cancellationToken)
    {
        return config.BeginConfigMutationScopeAsync((value, token) =>
        {
            var updated = value with { LastKnownStatus = isOnline };
            return config.SaveAsync(updated, token);
        }, cancellationToken);
    }

    private record NetworkConfig(bool GameClose);
}