using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility;

public static class ReactionExtensions
{
    extension(Event<IncomingMessage> message)
    {
        public ValueTask Reaction(IBotContext bot, string reaction, bool add,
            CancellationToken cancellationToken = default)
        {
            return bot.UpdateGroupReactionAsync(message.SelfId, message.Data.PeerId, message.Data.MessageSeq, reaction,
                add, cancellationToken);
        }

        public ValueTask AddReaction(IBotContext bot, string reaction,
            CancellationToken cancellationToken = default)
        {
            return message.Reaction(bot, reaction, true, cancellationToken);
        }
        
        public ValueTask RemoveReaction(IBotContext bot, string reaction,
            CancellationToken cancellationToken = default)
        {
            return message.Reaction(bot, reaction, false, cancellationToken);
        }
    }
}