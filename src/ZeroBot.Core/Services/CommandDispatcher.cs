using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;

namespace ZeroBot.Core.Services;

public class CommandDispatcher(IBotContext botContext, ILogger<CommandDispatcher> logger)
    : ICommandDispatcher, IInfrastructureInitializer
{
    private readonly Dictionary<string, CommandHandlerMetadata> _idToHandlers = [];
    private readonly CancellationTokenSource _cts = new();
    
    public Registration RegisterCommand(CommandHandlerMetadata commandHandlerMetadata)
    {
        var id = commandHandlerMetadata.Id ?? Guid.NewGuid().ToString();
        if (!_idToHandlers.TryAdd(id, commandHandlerMetadata)) throw new InvalidOperationException($"Command handler with id {id} already exists.");

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
            if (!message.Data.ToText().Trim().StartsWith('/')) continue;
            
            var handler = await _idToHandlers.Values.ToAsyncEnumerable()
                .FirstOrDefaultAsync(async (handler, token) => await handler.Predicate(message, token), initCts.Token);
            
            if (handler == null) continue;

            try
            {
                await handler.Handler(message, initCts.Token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while processing the command");
            }
        }
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        logger.LogInformation("Starting command dispatcher...");
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