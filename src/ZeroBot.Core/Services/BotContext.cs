using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Core.Services;

public class BotContext(ILogger<BotContext> logger) : IBotContext
{
    private readonly Dictionary<long, IBotService> _services = new();
    
    public IEnumerable<IBotService> BotServices => _services.Values;

    private readonly Dictionary<Guid, ChannelWriter<Event>> _subscribers = [];
    
    public async IAsyncEnumerable<Event> ReadEvents(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<Event>();
        _subscribers.Add(id, channel.Writer);
        await foreach (var @event in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return @event;
        }
        _subscribers.Remove(id);
    }

    public async ValueTask WriteEvent(Event @event, CancellationToken cancellationToken = default)
    {
        if (EventRepository is not null)
        {
            try
            {
                await EventRepository.SaveEventAsync(@event.SelfId, @event, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while saving the event.");
            }
        }
        foreach (var writer in _subscribers.Values)
        {
            await writer.WriteAsync(@event, cancellationToken);
        }
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

    public ValueTask<string> GetTempResourceUrlAsync(long accountId, string id,
        CancellationToken cancellationToken = default)
    {
        return !_services.TryGetValue(accountId, out var botService)
            ? default
            : botService.GetTempResourceUrlAsync(id, cancellationToken);
    }

    public async ValueTask UpdateGroupReactionAsync(long accountId, long groupId, long messageId, string reactionId, bool add,
        CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(accountId, out var botService)) return;

        await botService.UpdateGroupReactionAsync(groupId, messageId, reactionId, add, cancellationToken);
    }
}
