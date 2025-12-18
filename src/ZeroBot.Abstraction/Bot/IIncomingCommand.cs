namespace ZeroBot.Abstraction.Bot;

public interface IIncomingCommand
{
    string Name { get; }
    string[] Arguments { get; }
    T? ParseNextArgument<T>(Func<string, T> parser);
    bool HasNext { get; }
}
