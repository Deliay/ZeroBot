using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin;

public class PingCommand(ICommandDispatcher dispatcher, IBotContext bot) : CommandHandler(dispatcher)
{
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.Data.ToText() == "/ping");
    }
    
    protected override async ValueTask HandleAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        await bot.WriteManyGroupMessageAsync(message.SelfId, [message.Data.PeerId], cancellationToken,
            message.Data.Reply(), "pong".ToSegment());
    }
}
