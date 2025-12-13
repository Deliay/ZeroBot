using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Core.Services;

public class BotContext : IBotContext
{
    private readonly Dictionary<long, IBotService> _services = new();
    private readonly Channel<Event> _messageQueue = Channel.CreateUnbounded<Event>();
    
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
    }

    public async ValueTask UnregisterBot(IBotService botService, CancellationToken cancellationToken = default)
    {
        var account = await botService.GetCurrentAccountAsync(cancellationToken);
        _services.Remove(account.Uin, out _);
    }
}