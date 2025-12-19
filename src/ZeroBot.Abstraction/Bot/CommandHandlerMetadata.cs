using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public delegate ValueTask CommandHandle(Event<IncomingMessage> message, CancellationToken cancellationToken = default);
public delegate ValueTask<bool> CommandPredicate(Event<IncomingMessage> message, CancellationToken cancellationToken = default);

public record CommandHandlerMetadata(CommandHandle Handler, CommandPredicate Predicate, string? Id = null);
