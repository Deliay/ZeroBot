using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public record CommandHandler(
    Delegate Handler,
    Func<ITextCommand, Event<IncomingMessage>, ValueTask<bool>> Predictor,
    string? Id = null,
    IEnumerable<string>? Aliases = null);
