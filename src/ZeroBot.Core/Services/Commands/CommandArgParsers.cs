namespace ZeroBot.Core.Services.Commands;

public static class CommandArgParsers
{
    private static readonly Dictionary<Type, Delegate> _parsers = new();

    public static void RegisterParser<T>(Func<string, T> parser)
    {
        _parsers.Add(typeof(T), parser);
    }
    
    public static bool HasParser(Type type) => _parsers.ContainsKey(type);
    
    public static Delegate GetParser(Type type) => _parsers[type];
}
