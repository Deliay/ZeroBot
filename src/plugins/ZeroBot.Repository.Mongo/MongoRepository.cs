using System.Linq.Expressions;
using Milky.Net.Model;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Repository.Mongo;

public class MongoRepository(IMongoClient mongo) : IBotEventRepository
{
    static MongoRepository()
    {
        IgnoreIdField.Register();
        BsonClassMap.RegisterClassMap<ImageIncomingSegment>();
    }
    
    public IAsyncEnumerable<Event<T>> SearchEventAsync<T>(long accountId, Expression<Func<Event<T>, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return mongo.GetDatabase(MongoRepositoryExtensions.GetEventDatabase(accountId))
            .GetCollection<Event<T>>(typeof(T).Name)
            .Find(predicate).ToAsyncEnumerable();
    }

    public async ValueTask SaveEventAsync(long accountId, Event @event, CancellationToken cancellationToken)
    {
        await @event.InsertAsync(mongo, accountId, cancellationToken);
    }
}
