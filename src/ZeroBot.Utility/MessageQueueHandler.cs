using System.Threading.Channels;
using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility;

public abstract class MessageQueueHandler<T>(IBotContext bot, ILogger<T> logger)
    : IExecutable where T : MessageQueueHandler<T>
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

    private async ValueTask RunAsyncCore(CancellationToken cancellationToken = default)
    {
        await InitializeHandler(cancellationToken);
        var enqueueTask = StartQueueAsync(cancellationToken);
        var dequeueTask = StartDequeueAsync(cancellationToken);
        
        await Task.WhenAll(enqueueTask, dequeueTask);
    }
    
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsyncCore(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while processing the message queue.");
        }
    }
}