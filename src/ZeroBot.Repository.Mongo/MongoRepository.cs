using System.Linq.Expressions;
using Milky.Net.Model;
using MongoDB.Driver;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Repository.Mongo;

public class MongoRepository(IMongoClient mongo) : IBotEventRepository
{
    public IAsyncEnumerable<Event<T>> SearchEventAsync<T>(long accountId, Expression<Func<Event<T>, bool>> predictor,
        CancellationToken cancellationToken) where T : Event
    {
        return mongo.GetDatabase(MongoRepositoryExtensions.GetEventDatabase(accountId))
            .GetCollection<Event<T>>(nameof(T))
            .Find(predictor).ToAsyncEnumerable();
    }

    public async ValueTask SaveEventAsync(long accountId, Event @event, CancellationToken cancellationToken)
    {
        await @event.InsertAsync(mongo, accountId, cancellationToken);
    }
}
