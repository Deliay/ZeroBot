using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility.Commands;

public static class TextCommandExtensions
{
    private static T Parse<T>(string data) where T : IParsable<T> => T.Parse(data, CultureInfo.InvariantCulture);
    private static Delegate Of(Delegate @delegate) => @delegate;
    private static readonly MethodInfo ParseableParserMethod = Of(Parse<int>).Method.GetGenericMethodDefinition();
    private static readonly Dictionary<Type, Delegate> ParseableMethodCache = new();
    
    private static Delegate MakeParseableMethod(Type type)
    {
        if (ParseableMethodCache.TryGetValue(type, out var method)) return method;
        if (CommandArgParsers.HasParser(type)) return CommandArgParsers.GetParser(type);
        if (!type.IsAssignableTo(typeof(IParsable<>).MakeGenericType(type)))
            throw new InvalidOperationException(
                $"Type {type} is not parseable. Consider registering a parser in CommandArgParsers or inheriting IParsable<T>.");

        var delegateType = typeof(Func<,>).MakeGenericType([typeof(string), type]);
        method = ParseableParserMethod.MakeGenericMethod(type).CreateDelegate(delegateType);
        ParseableMethodCache.Add(type, method);
        return method;
    }

    private static IEnumerable<MethodCallExpression> GetArguments(MethodInfo method,
        Expression argsExpr, params Type[] ignoredTypes)
    {
        var parameters = method.GetParameters();
        foreach (var parameterInfo in parameters.OrderBy(p => p.Position))
        {
            if (ignoredTypes.Contains(parameterInfo.ParameterType)) continue;

            var parseMethod = MakeParseableMethod(parameterInfo.ParameterType);
            var paramExpr = Expression.ArrayIndex(argsExpr, Expression.Constant(parameterInfo.Position));
            var callExpr = Expression.Call(parseMethod.Method, paramExpr);
            yield return callExpr;
        }
    }
    
    private static readonly Dictionary<Delegate, Action<string[]>> SyncHandlerCache = new();
    private static readonly Dictionary<Delegate, Func<string[], CancellationToken, Task>> AsyncHandlerCache = new();

    public static Action<string[]> GetInvoker(Delegate @delegate)
    {
        if (SyncHandlerCache.TryGetValue(@delegate, out var handler)) return handler;
        var method = @delegate.Method;
        var instanceExpr = method.IsStatic ? null : Expression.Constant(@delegate.Target);
        
        var argsExpr = Expression.Parameter(typeof(string[]), "args");
        var arguments = GetArguments(method, argsExpr);
        var callExpr = Expression.Call(instanceExpr, method, arguments);
        handler = Expression.Lambda<Action<string[]>>(callExpr, argsExpr).Compile();
        SyncHandlerCache.Add(@delegate, handler);
        return handler;
    }

    public static Func<string[], CancellationToken, Task> GetAsyncInvoker(Delegate @delegate)
    {
        if (AsyncHandlerCache.TryGetValue(@delegate, out var handler)) return handler;
        var method = @delegate.Method;
        var instanceExpr = method.IsStatic ? null : Expression.Constant(@delegate.Target);
        
        var argsExpr = Expression.Parameter(typeof(string[]), "args");
        var cancellationTokenExpr = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var arguments = GetArguments(method, argsExpr, typeof(CancellationToken));
        var callExpr = Expression.Call(instanceExpr, method, [..arguments, cancellationTokenExpr]);
        handler = Expression.Lambda<Func<string[], CancellationToken, Task>>(
            callExpr, argsExpr, cancellationTokenExpr).Compile();
        AsyncHandlerCache.Add(@delegate, handler);
        return handler;
    }
    
    extension(ITextCommand command)
    {
        public T? ParseNextArgument<T>() where T : IParsable<T>
        {
            return command.ParseNextArgument<T>((data) => T.Parse(data, CultureInfo.InvariantCulture));
        }
        
        public void InvokeCommand(Delegate @delegate)
        {
            if (@delegate.Method.GetParameters().Length != command.Arguments.Length)
                throw new InvalidOperationException("Handle method must have the same number of arguments as the command.");
            
            GetInvoker(@delegate)(command.Arguments);
        }

        public Task InvokeCommandAsync(Delegate @delegate, CancellationToken cancellationToken = default)
        {
            if (@delegate.Method.ReturnType != typeof(Task))
                throw new InvalidOperationException("Handle method must return Task.");
            
            if (@delegate.Method.GetParameters().Length - 1 != command.Arguments.Length)
                throw new InvalidOperationException("Handle method must have the same number of arguments as the command (without CancellationToken).");
            
            return GetAsyncInvoker(@delegate)(command.Arguments, cancellationToken);
        }
    }
}
