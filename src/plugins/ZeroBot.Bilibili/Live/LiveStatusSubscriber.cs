using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using Mikibot.Crawler.Http.Bilibili;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class LiveStatusSubscriber(
    IJsonConfig<BilibiliOptions> config,
    BiliLiveCrawler crawler,
    ILogger<LiveStatusSubscriber> logger,
    IBotContext bot) : IExecutable
{
    private readonly Random _random = new();
    
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await config.WaitForInitializedAsync(cancellationToken);
            foreach (var (strRoomId, targetGroups) in config.Current.RoomIdToGroupSubscriptions)
            {
                try
                {
                    if (targetGroups.Count == 0) continue;
                    var roomId = long.Parse(strRoomId);
                    var info = await crawler.GetLiveRoomInfo(roomId, cancellationToken);
                    var streaming = info.LiveStatus == 1;
                    // if not initialized, set status only, don't send notifcation
                    var initialized = config.Current.LastLiveStatus.TryGetValue(strRoomId, out var liveStatus);
                    // if current status sames with saved status, skip
                    if (streaming == liveStatus) continue;
                    // update current live status
                    await config.BeginConfigMutationScopeAsync(async (value, token) =>
                    {
                        value.LastLiveStatus.Remove(strRoomId);
                        value.LastLiveStatus.TryAdd(strRoomId, streaming);
                        await config.SaveAsync(value, token);
                    }, cancellationToken);
                    
                    // only send notification when status was initialized
                    if (initialized)
                    {
                        var status = streaming ? "开" : "下";
                        var url = streaming ? $"https://live.bilibili.com/{info.RoomId}" : "";
                        await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
                        {
                            foreach (var targetGroup in targetGroups)
                            {
                                var atAll = streaming ? await bot.TryAtAllMembers(accountId, targetGroup, cancellationToken) : [];
                                await bot.WriteManyGroupMessageAsync(accountId, targetGroups, cancellationToken,
                                [
                                    info.UserCover.ToMilkyImageSegment(),
                                    ..atAll,
                                    $"{status}啦~\n{info.Title} {url}".ToMilkyTextSegment(),
                                ]);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "LiveStatusSubscriber Exception");
                }

                await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 5)), cancellationToken);
            }
            await Task.Delay(TimeSpan.FromSeconds(_random.Next(10, 15)), cancellationToken);
        }
    }
}