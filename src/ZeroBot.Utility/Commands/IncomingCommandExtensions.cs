using System.Globalization;

namespace ZeroBot.Utility.Commands;

public static class IncomingCommandExtensions
{
    extension(IIncomingCommand command)
    {
        public T? ParseNextArgument<T>() where T : IParsable<T>
        {
            return command.ParseNextArgument<T>((data) => T.Parse(data, CultureInfo.InvariantCulture));
        }
    }
}
