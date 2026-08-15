using System.Collections.Concurrent;
using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili.Live;

public class LiveScSubscriber(
    IJsonConfig<BilibiliOptions> config,
    LiveScApi api,
    ILogger<LiveScSubscriber> logger,
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
                logger.LogError(e, "LiveScSubscriber reconcile exception");
            }
        }
        foreach (var (_, cts) in _activeRooms)
            cts.Cancel();
    }

    private void Reconcile(CancellationToken cancellationToken)
    {
        var current = config.Current.ScRoomIdToGroupSubscriptions;
        foreach (var (roomId, groups) in current)
        {
            if (groups.Count == 0) continue;
            if (_activeRooms.ContainsKey(roomId)) continue;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_activeRooms.TryAdd(roomId, cts))
                _ = ReceiveLoopAsync(roomId, cts.Token);
        }
        foreach (var roomId in _activeRooms.Keys.ToArray())
        {
            if (current.TryGetValue(roomId, out var groups) && groups.Count > 0) continue;
            if (_activeRooms.TryRemove(roomId, out var cts))
                cts.Cancel();
        }
    }

    private async Task ReceiveLoopAsync(string roomId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await api.ReceiveSuperChatEventsAsync(roomId,
                    (sc, ct) => ForwardSuperChatAsync(roomId, sc, ct), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "LiveScSubscriber receive exception, roomId: {RoomId}", roomId);
            }
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ForwardSuperChatAsync(string roomId, SuperChatData sc, CancellationToken cancellationToken)
    {
        if (!config.Current.ScRoomIdToGroupSubscriptions.TryGetValue(roomId, out var groups) || groups.Count == 0)
            return;
        var segments = SuperChatMessageBuilder.Build(sc);
        await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
        {
            await bot.WriteManyGroupMessageAsync(accountId, groups, cancellationToken, segments);
        }
    }
}
