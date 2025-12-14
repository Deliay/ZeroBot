namespace ZeroBot.Utility.Commands;

public interface IIncomingCommand
{
    string Name { get; }
    T? ParseNextArgument<T>(Func<string, T> parser);
    bool HasNext { get; }
}
