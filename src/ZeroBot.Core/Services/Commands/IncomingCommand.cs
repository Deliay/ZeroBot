using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Core.Services.Commands;

internal record IncomingCommand(string Name, string[] Arguments) : IIncomingCommand
{
    private int Position { get; set; } = 0;

    public T? ParseNextArgument<T>(Func<string, T> parser)
    {
        if (Position >= Arguments.Length || !HasNext)
        {
            return default;
        }
        var current = Arguments[Position++];
        return parser(current);
    }

    public bool HasNext { get; private set; } = Arguments.Length != 0;
}
