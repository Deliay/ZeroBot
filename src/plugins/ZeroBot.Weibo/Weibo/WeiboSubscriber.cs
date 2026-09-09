using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Weibo.Weibo;

public class WeiboSubscriber(
    IJsonConfig<WeiboOptions> config,
    WeiboApi api,
    ILogger<WeiboSubscriber> logger,
    IBotContext bot) : IExecutable
{
    private readonly Random _random = new();

    private async ValueTask RunAsyncCore(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        foreach (var (uid, targetGroups) in config.Current.UidToGroupSubscriptions)
        {
            try
            {
                if (targetGroups.Count == 0) continue;
                var item = await api.GetLatestWeiboAsync(uid, cancellationToken);
                // fetch failure or empty space: keep polling in the next round
                if (item?.Data == null) continue;
                config.Current.LastWeiboIds.TryGetValue(uid, out var lastMblogId);
                // same weibo as last time, skip
                if (lastMblogId == item.MblogId) continue;
                // update current mblog id
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.LastWeiboIds.Remove(uid);
                    value.LastWeiboIds.TryAdd(uid, item.MblogId);
                    await config.SaveAsync(value, token);
                }, cancellationToken);

                // only send notification when the last id was recorded and changed
                if (!string.IsNullOrEmpty(lastMblogId))
                {
                    var segments = WeiboMessageBuilder.Build(item);
                    await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
                    {
                        await bot.WriteManyGroupMessageAsync(accountId, targetGroups, cancellationToken, segments);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "WeiboSubscriber Exception");
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
                logger.LogError(e, "WeiboSubscriber Exception");
            }
        }
    }
}