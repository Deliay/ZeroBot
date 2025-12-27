using System.Threading.Channels;
using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility;

public abstract class MessageQueueHandler(IBotContext bot) : IExecutable
{
    private readonly Channel<Event<IncomingMessage>> _processQueue = Channel.CreateUnbounded<Event<IncomingMessage>>();
    protected abstract ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default);
    
    private async Task StartDequeueAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var @event in _processQueue.Reader.ReadAllAsync(cancellationToken))
        {
            await DequeueAsync(@event, cancellationToken);
        }
    }
    
    private async Task StartQueueAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var @event in bot.ReadEvents(cancellationToken)
                           .OfType<Event<IncomingMessage>>()
                           .WithCancellation(cancellationToken))
        {
            await _processQueue.Writer.WriteAsync(@event, cancellationToken);
        }
    }

    protected virtual ValueTask InitializeHandler(CancellationToken cancellationToken = default) => default;

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await InitializeHandler(cancellationToken);
        var enqueueTask = StartQueueAsync(cancellationToken);
        var dequeueTask = StartDequeueAsync(cancellationToken);
        
        await Task.WhenAll(enqueueTask, dequeueTask);
    }
}