using EmberFramework.Abstraction.Layer.Plugin;
using Milky.Net.Model;
using ZeroBot.Abstraction.Service;

namespace ZeroBot.Abstraction.Bot;

public abstract class CommandHandler(ICommandDispatcher dispatcher) : IComponentInitializer
{
    private Registration? _registration;

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _registration = dispatcher.RegisterCommand(new CommandHandlerMetadata(HandleAsync, PredicateAsync));
        return InitializeCommandAsync(cancellationToken);
    }

    protected virtual ValueTask InitializeCommandAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    protected abstract ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default);

    protected abstract ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default);

    public void Dispose()
    {
        _registration?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _registration?.Dispose();
        return ValueTask.CompletedTask;
    }
}
