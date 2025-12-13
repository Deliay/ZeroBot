using System.Linq.Expressions;
using Milky.Net.Model;

namespace ZeroBot.Abstraction.Bot;

public interface IBotEventRepository
{
    IAsyncEnumerable<T> SearchEventAsync<T>(Expression<Func<T, bool>> predictor, CancellationToken cancellationToken)
        where T : Event;

    ValueTask SaveEventAsync(Event @event, CancellationToken cancellationToken);
}
