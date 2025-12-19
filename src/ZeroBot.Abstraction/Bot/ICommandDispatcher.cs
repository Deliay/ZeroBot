using ZeroBot.Abstraction.Service;

namespace ZeroBot.Abstraction.Bot;

public interface ICommandDispatcher
{
    public Registration RegisterCommand(CommandHandlerMetadata commandHandlerMetadata);
}
