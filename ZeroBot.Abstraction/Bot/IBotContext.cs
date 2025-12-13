using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotContext
{
    IAsyncEnumerable<Event> ReadEvents(CancellationToken cancellationToken = default);

    ValueTask WriteEvent(Event @event, CancellationToken cancellationToken = default);
    
    ValueTask<MultiGroupSendResult> WriteManyGroupMessageAsync(long accountId, HashSet<long> groupIds,
        CancellationToken cancellationToken = default, params OutgoingSegment[] messageSegments);

    IAsyncEnumerable<GetLoginInfoOutput> GetAccountInfoAsync(CancellationToken cancellationToken = default);
    
    ValueTask RegisterBotAsync(IBotService botService, CancellationToken cancellationToken = default);
    ValueTask UnregisterBot(IBotService botService, CancellationToken cancellationToken = default);
    
    public IBotEventRepository EventRepository { get; }
    
    void SetEventRepository(IBotEventRepository repository);
}
