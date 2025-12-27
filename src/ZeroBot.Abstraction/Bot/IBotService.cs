using EmberFramework.Abstraction;
using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotService : IExecutable
{
    
    ValueTask<GetLoginInfoOutput> GetCurrentAccountAsync(CancellationToken cancellationToken = default);
    
    ValueTask<MultiGroupSendResult> SendGroupMessageAsync(HashSet<long> groupIds,
        CancellationToken cancellationToken = default,
        params OutgoingSegment[] messageSegments);

    ValueTask<Event<IncomingMessage>> GetGroupMessageAsync(MessageScene scene, long peerId, long messageId,
        CancellationToken cancellationToken = default);

    ValueTask UpdateGroupReactionAsync(long groupId, long messageId, string reactionId, bool add,
        CancellationToken cancellationToken = default);

    ValueTask<GetGroupInfoOutput> GetGroupInformationAsync(long groupId,
        CancellationToken cancellationToken = default);

    ValueTask<GetGroupMemberListOutput?> GetGroupMembersAsync(long groupId,
        CancellationToken cancellationToken = default);

    ValueTask<string> GetTempResourceUrlAsync(string id, CancellationToken cancellationToken = default);
}
