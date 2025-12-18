using EmberFramework.Abstraction.Layer.Plugin;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Core.Services;

public class CommandDispatcher(IBotContext botContext) : ICommandDispatcher, IComponentInitializer
{
    private readonly Dictionary<string, CommandHandler> _idToHandlers = [];
    private readonly CancellationTokenSource _cts = new();
    
    public Registration RegisterCommand(CommandHandler commandHandler)
    {
        var id = commandHandler.Id ?? Guid.NewGuid().ToString();
        if (!_idToHandlers.TryAdd(id, commandHandler)) throw new InvalidOperationException($"Command handler with id {id} already exists.");

        return new Registration(() =>
        {
            _idToHandlers.Remove(id);
        });
    }

    private async Task RunCommandDispatcherAsync(CancellationToken cancellationToken)
    {
        using var initCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var messages = botContext.ReadEvents(initCts.Token)
            .OfType<Event<IncomingMessage>>()
            .WithCancellation(initCts.Token);

        await foreach (var message in messages)
        {
            if (message.Data.Segments[0] is not TextIncomingSegment segment) continue;
            if (!segment.Data.Text.StartsWith('/')) continue;
            
            var handler = await _idToHandlers.Values.ToAsyncEnumerable()
                .FirstOrDefaultAsync(async (handler, token) => await handler.Predicate(message, token), initCts.Token);
            
            if (handler == null) continue;
            
            await handler.Handler(message, initCts.Token);
        }
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        _ = RunCommandDispatcherAsync(cancellationToken);
        return default;
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _cts.Dispose();
        return default;
    }
}