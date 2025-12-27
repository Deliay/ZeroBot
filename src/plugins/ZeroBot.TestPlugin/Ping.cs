using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin;

public class Ping(ICommandDispatcher dispatcher, IBotContext bot) : CommandQueuedHandler(dispatcher)
{
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.Data.ToText() == "/ping");
    }
    
    protected override ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
       return @event.ReplyAsGroup(bot, cancellationToken, ["pong".ToMilkyTextSegment()]);
    }
}
