using EmberFramework.Abstraction;
using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotService : IExecutable
{
    
    ValueTask<GetLoginInfoOutput> GetCurrentAccountAsync(CancellationToken cancellationToken = default);
    
    ValueTask<MultiGroupSendResult> SendGroupMessageAsync(HashSet<long> groupIds,
        CancellationToken cancellationToken = default,
        params OutgoingSegment[] messageSegments);
}
