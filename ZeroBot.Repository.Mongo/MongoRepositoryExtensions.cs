using System.Linq.Expressions;
using System.Reflection;
using Milky.Net.Model;
using MongoDB.Driver;

namespace ZeroBot.Repository.Mongo;

public static class MongoRepositoryExtensions
{
    private static readonly InsertOneOptions EmptyInsertOptions = new();

    private static async ValueTask SaveEventAsyncCore<T>(IMongoClient mongo, long accountId, Event<T> @event, CancellationToken cancellationToken)
    {
        await mongo.GetDatabase($"events-{accountId}")
            .GetCollection<Event<T>>(nameof(T))
            .InsertOneAsync(@event, EmptyInsertOptions, cancellationToken);
    }

    private static Delegate Of(Delegate @delegate) => @delegate;

    private static readonly MethodInfo RawMethod =
        Of(SaveEventAsyncCore<int>).Method.GetGenericMethodDefinition();
    private static readonly Dictionary<Type, InsertEventDelegate> InsertFnCache = [];

    private delegate Task InsertEventDelegate(IMongoClient mongoClient, long accountId, Event @event,
        CancellationToken cancellationToken);
    
    private static InsertEventDelegate MakeInsertMethod(Event @event)
    {
        var dataType = @event.GetType().GetGenericArguments()[0];
        if (InsertFnCache.TryGetValue(dataType, out var method)) return method;
        var mongoClientParam = Expression.Parameter(typeof(IMongoClient), "mongo");
        var accountIdParam = Expression.Parameter(typeof(long), "accountId");
        var eventParam = Expression.Parameter(@event.GetType(), "event");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        
        var eventParamCast = Expression.Convert(eventParam, @event.GetType());
        var call = Expression.Call(RawMethod, mongoClientParam, accountIdParam, eventParamCast, cancellationTokenParam);
        
        method = Expression.Lambda<InsertEventDelegate>(call, mongoClientParam,
                accountIdParam, eventParam, cancellationTokenParam)
            .Compile();
        InsertFnCache.Add(dataType, method);
        return method;
    }
    
    extension(Event @event)
    {
        private InsertEventDelegate GetInsertMethod() => MakeInsertMethod(@event);

        public Task InsertAsync(IMongoClient mongoClient, long accountId,
            CancellationToken cancellationToken)
        {
            return @event.GetInsertMethod()(mongoClient, accountId, @event, cancellationToken);
        }
    }
}
