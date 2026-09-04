using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Dynamic;

public class DynamicSubscriber(
    IJsonConfig<BilibiliOptions> config,
    VtuberSpaceApi api,
    ILogger<DynamicSubscriber> logger,
    IBotContext bot) : IExecutable
{
    private readonly Random _random = new();

    private async ValueTask RunAsyncCore(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        foreach (var (mid, targetGroups) in config.Current.MidToGroupSubscriptions)
        {
            try
            {
                if (targetGroups.Count == 0) continue;
                var item = await api.GetLatestDynamicAsync(mid, cancellationToken);
                // fetch failure or empty space: keep polling in the next round
                if (item?.Data == null) continue;
                config.Current.LastDynamicIds.TryGetValue(mid, out var lastDynamicId);
                // same dynamic as last time, skip
                if (lastDynamicId == item.DynamicId) continue;
                // update current dynamic id
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.LastDynamicIds.Remove(mid);
                    value.LastDynamicIds.TryAdd(mid, item.DynamicId);
                    await config.SaveAsync(value, token);
                }, cancellationToken);

                // only send notification when the last id was recorded and changed
                if (!string.IsNullOrEmpty(lastDynamicId))
                {
                    // skip live recommend dynamics, don't forward to QQ groups
                    if (item.Data?.Type == "DYNAMIC_TYPE_LIVE_RCMD") continue;

                    var segments = DynamicMessageBuilder.Build(item.Data, mid);
                    await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
                    {
                        await bot.WriteManyGroupMessageAsync(accountId, targetGroups, cancellationToken, segments);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "DynamicSubscriber Exception");
            }

            await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 3)), cancellationToken);
        }
        await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunAsyncCore(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "DynamicSubscriber Exception");
            }
        }
    }
}
