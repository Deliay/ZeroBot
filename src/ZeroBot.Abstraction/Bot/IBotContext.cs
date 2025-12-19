using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotContext
{
    IAsyncEnumerable<Event> ReadEvents(CancellationToken cancellationToken = default);

    ValueTask WriteEvent(Event @event, CancellationToken cancellationToken = default);
    
    ValueTask<MultiGroupSendResult> WriteManyGroupMessageAsync(long accountId, HashSet<long> groupIds,
        CancellationToken cancellationToken = default, params OutgoingSegment[] messageSegments);

    ValueTask<Event<IncomingMessage>?> GetHistoryMessageAsync(long accountId, MessageScene scene, long peerId, long messageId,
        CancellationToken cancellationToken = default);

    ValueTask UpdateGroupReactionAsync(long accountId, long groupId, long messageId, string reactionId, bool add,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<GetLoginInfoOutput> GetAccountInfoAsync(CancellationToken cancellationToken = default);

    ValueTask<GetGroupInfoOutput?> GetGroupInformationAsync(long accountId, long groupId,
        CancellationToken cancellationToken = default);
    
    ValueTask<GetGroupMemberListOutput?> GetGroupMembersAsync(long accountId, long groupId,
        CancellationToken cancellationToken = default);
    
    ValueTask RegisterBotAsync(IBotService botService, CancellationToken cancellationToken = default);
    ValueTask UnregisterBot(IBotService botService, CancellationToken cancellationToken = default);
    
    public IBotEventRepository? EventRepository { get; }
    
    void SetEventRepository(IBotEventRepository repository);
}
