using System.Collections.Concurrent;
using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class AnchorEventSubscriber(
    IJsonConfig<BilibiliOptions> config,
    AnchorEventApi api,
    ILogger<AnchorEventSubscriber> logger,
    IBotContext bot) : IExecutable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRooms = new();

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Reconcile(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "AnchorEventSubscriber reconcile exception");
            }
        }
        foreach (var (_, cts) in _activeRooms)
            cts.Cancel();
    }

    private void Reconcile(CancellationToken cancellationToken)
    {
        var current = config.Current.AnchorEventSubscriptions;
        var activeRoomIds = current.Values
            .Where(s => s.GroupIds.Count > 0)
            .Select(s => s.RoomId)
            .Distinct()
            .ToHashSet();

        foreach (var roomId in activeRoomIds)
        {
            if (_activeRooms.ContainsKey(roomId)) continue;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_activeRooms.TryAdd(roomId, cts))
            {
                _ = ReceiveDanmakuLoopAsync(roomId, cts.Token);
                _ = ReceiveInteractLoopAsync(roomId, cts.Token);
            }
        }

        foreach (var roomId in _activeRooms.Keys.ToArray())
        {
            if (activeRoomIds.Contains(roomId)) continue;
            if (_activeRooms.TryRemove(roomId, out var cts))
                cts.Cancel();
        }
    }

    private async Task ReceiveDanmakuLoopAsync(string roomId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await api.ReceiveDanmakuEventsAsync(roomId,
                    (msg, ct) => ForwardDanmakuAsync(roomId, msg, ct), cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "AnchorEventSubscriber receive danmaku exception, roomId: {RoomId}", roomId);
            }
        }
    }

    private async Task ReceiveInteractLoopAsync(string roomId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await api.ReceiveInteractEventsAsync(roomId,
                    (enter, ct) => ForwardInteractAsync(roomId, enter, ct), cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "AnchorEventSubscriber receive interact exception, roomId: {RoomId}", roomId);
            }
        }
    }

    private async Task ForwardDanmakuAsync(string roomId, Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand.DanmuMsg msg, CancellationToken cancellationToken)
    {
        var current = config.Current.AnchorEventSubscriptions;
        foreach (var (mid, subscription) in current)
        {
            if (subscription.RoomId != roomId) continue;
            if (subscription.GroupIds.Count == 0) continue;
            if (msg.UserId.ToString() != mid) continue;

            var segments = AnchorEventMessageBuilder.BuildDanmaku(subscription.UName, msg);
            await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
            {
                await bot.WriteManyGroupMessageAsync(accountId, subscription.GroupIds, cancellationToken, segments);
            }
        }
    }

    private async Task ForwardInteractAsync(string roomId, Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand.ProtoCommand.EnterRoomEvent enter, CancellationToken cancellationToken)
    {
        var current = config.Current.AnchorEventSubscriptions;
        foreach (var (mid, subscription) in current)
        {
            if (subscription.RoomId != roomId) continue;
            if (subscription.GroupIds.Count == 0) continue;
            if (enter.Uid.ToString() != mid) continue;

            var segments = AnchorEventMessageBuilder.BuildEnter(subscription.UName);
            await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
            {
                await bot.WriteManyGroupMessageAsync(accountId, subscription.GroupIds, cancellationToken, segments);
            }
        }
    }
}
