using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Core.Services;

public class BotContext(ILogger<BotContext> logger) : IBotContext
{
    private readonly Dictionary<long, IBotService> _services = new();
    private readonly Channel<Event> _messageQueue = Channel.CreateUnbounded<Event>();
    
    public IEnumerable<IBotService> BotServices => _services.Values;
    
    public IAsyncEnumerable<Event> ReadEvents(CancellationToken cancellationToken = default)
    {
        return _messageQueue.Reader.ReadAllAsync(cancellationToken);
    }

    public async ValueTask WriteEvent(Event @event, CancellationToken cancellationToken = default)
    {
        await _messageQueue.Writer.WriteAsync(@event, cancellationToken);
    }

    public async ValueTask<MultiGroupSendResult> WriteManyGroupMessageAsync(long accountId, HashSet<long> groupIds,
        CancellationToken cancellationToken = default, params OutgoingSegment[] messageSegments)
    {
        if (!_services.TryGetValue(accountId, out var botService))
        {
            return new MultiGroupSendResult();
        }
        return await botService.SendGroupMessageAsync(groupIds, cancellationToken, messageSegments);
    }

    public async ValueTask<GetGroupInfoOutput?> GetGroupInformationAsync(long accountId, long groupId,
        CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(accountId, out var botService))
        {
            return null;
        }

        return await botService.GetGroupInformationAsync(groupId, cancellationToken);
    }
    
    public async ValueTask<GetGroupMemberListOutput?> GetGroupMembersAsync(long accountId, long groupId,
        CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(accountId, out var botService))
        {
            return null;
        }
        
        return await botService.GetGroupMembersAsync(groupId, cancellationToken);
    }

    public async ValueTask<Event<IncomingMessage>?> GetHistoryMessageAsync(long accountId, MessageScene scene, long peerId, long messageId,
        CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(accountId, out var botService)) return null;

        return await botService.GetGroupMessageAsync(scene, peerId, messageId, cancellationToken);
    }

    public async IAsyncEnumerable<GetLoginInfoOutput> GetAccountInfoAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var botService in _services.Values)
        {
            yield return await botService.GetCurrentAccountAsync(cancellationToken);
        }
    }

    public async ValueTask RegisterBotAsync(IBotService botService, CancellationToken cancellationToken = default)
    {
        var account = await botService.GetCurrentAccountAsync(cancellationToken);
        if (!_services.TryAdd(account.Uin, botService))
        {
            throw new InvalidOperationException(
                $"Bot {botService.GetType().Name} with account {account.Nickname}({account.Uin}) already registered.");
        }
        
        logger.LogInformation("Bot {NickName}({Uid}) was registered.", account.Nickname, account.Uin);
    }

    public async ValueTask UnregisterBot(IBotService botService, CancellationToken cancellationToken = default)
    {
        var account = await botService.GetCurrentAccountAsync(cancellationToken);
        _services.Remove(account.Uin, out _);
    }

    public IBotEventRepository? EventRepository { get; private set; }
    
    public void SetEventRepository(IBotEventRepository repository)
    {
        if (EventRepository is not null) throw new InvalidOperationException("Repository already set.");
        
        EventRepository = repository;
    }

    public async ValueTask UpdateGroupReactionAsync(long accountId, long groupId, long messageId, string reactionId, bool add,
        CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(accountId, out var botService)) return;

        await botService.UpdateGroupReactionAsync(groupId, messageId, reactionId, add, cancellationToken);
    }
}
