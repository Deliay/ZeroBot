using System.Linq.Expressions;
using Milky.Net.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Repository.Mongo;

public class MongoRepository(IMongoClient mongo) : IBotEventRepository
{
    public IAsyncEnumerable<Event<T>> SearchEventAsync<T>(long accountId, Expression<Func<Event<T>, bool>> predictor,
        CancellationToken cancellationToken) where T : Event
    {
        return mongo.GetDatabase($"events-{accountId}")
            .GetCollection<Event<T>>(nameof(T))
            .Find(predictor).ToAsyncEnumerable();
    }

    private static readonly InsertOneOptions EmptyInsertOptions = new(); 
    
    public async ValueTask SaveEventAsync(long accountId, Event @event, CancellationToken cancellationToken)
    {
        var eventType = @event.GetType().GetGenericArguments().First();
        await mongo.GetDatabase($"events-{accountId}")
            .GetCollection<BsonDocument>(eventType.Name)
            .InsertOneAsync(BsonDocument.Create(@event), EmptyInsertOptions, cancellationToken);
    }
}
