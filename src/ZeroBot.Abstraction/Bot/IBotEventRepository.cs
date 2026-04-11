using System.Linq.Expressions;
using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotEventRepository
{
    IAsyncEnumerable<Event<T>> SearchEventAsync<T>(long accountId,
        Expression<Func<Event<T>, bool>> predicate,
        CancellationToken cancellationToken = default);

    IQueryable<Event<T>> SearchEventAsync<T>(long accountId, int limit = 50);

    ValueTask SaveEventAsync(long accountId, Event @event, CancellationToken cancellationToken);
}
