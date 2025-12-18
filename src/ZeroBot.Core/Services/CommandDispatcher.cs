using EmberFramework.Abstraction.Layer.Plugin;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Core.Services;

public class CommandDispatcher(IBotContext botContext) : ICommandDispatcher, IComponentInitializer
{
    private readonly Dictionary<string, HashSet<string>> _commandHandlers = [];
    private readonly Dictionary<string, CommandHandler> _idToHandlers = [];
    private readonly CancellationTokenSource _cts = new();
    
    public Registration RegisterCommand(CommandHandler commandHandler)
    {
        var id = commandHandler.Id ?? Guid.NewGuid().ToString();
        if (_idToHandlers.ContainsKey(id)) throw new InvalidOperationException($"Command handler with id {id} already exists.");
        List<string> registerCommands = [];
        if (commandHandler.Aliases is not null)
        {
            foreach (var commandHandlerAlias in commandHandler.Aliases) 
            {
                if (commandHandlerAlias is { Length: > 0 }) registerCommands.Add(commandHandlerAlias);
            }   
        }
        foreach (var registerCommand in registerCommands)
        {
            if (!_commandHandlers.ContainsKey(registerCommand)) _commandHandlers.Add(registerCommand, []);
            _commandHandlers[registerCommand].Add(id);
        }
        
        _idToHandlers.Add(id, commandHandler);
        return new Registration(() =>
        {
            _idToHandlers.Remove(id);
            foreach (var registerCommand in registerCommands)
            {
                if (_commandHandlers.TryGetValue(registerCommand, out var handlers)) handlers.Remove(id);
            }
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

    public async ValueTask DisposeAsync()
    {
        if (_cts is IAsyncDisposable ctsAsyncDisposable)
            await ctsAsyncDisposable.DisposeAsync();
        else
            _cts.Dispose();
    }
}