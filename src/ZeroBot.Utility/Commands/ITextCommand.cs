namespace ZeroBot.Utility.Commands;

public interface ITextCommand
{
    string Name { get; }
    string[] Arguments { get; }
    T? ParseNextArgument<T>(Func<string, T> parser);
    bool HasNext { get; }
}
