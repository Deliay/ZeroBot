using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility;

public abstract class CommandQueuedHandler(ICommandDispatcher dispatcher)
    : CommandHandler(dispatcher)
{
    private readonly Channel<Event<IncomingMessage>> _processQueue = Channel.CreateUnbounded<Event<IncomingMessage>>();
    protected override async ValueTask HandleAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        await EnqueueInspectorAsync(@event, cancellationToken);
        await _processQueue.Writer.WriteAsync(@event, cancellationToken);
    }

    protected virtual ValueTask EnqueueInspectorAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask InitializeHandlerAsync(CancellationToken cancellationToken = default) => default;
    
    protected override async ValueTask InitializeCommandAsync(CancellationToken cancellationToken = default)
    {
        await InitializeHandlerAsync(cancellationToken);
        await base.InitializeCommandAsync(cancellationToken);
        _ = ProcessQueueAsync(cancellationToken);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var @event in _processQueue.Reader.ReadAllAsync(cancellationToken))
        {
            await DequeueAsync(@event, cancellationToken);
        }
    }
    
    protected abstract ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default);
}