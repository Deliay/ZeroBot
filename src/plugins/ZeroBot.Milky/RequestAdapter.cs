using System.Text.Json.Serialization.Metadata;
using Milky.Net.Client;

namespace ZeroBot.Milky;

public class RequestAdapter : IMilkyClientMiddleware
{
    public Task Execute<TRequest, TResponse>(string api, TRequest request,
        JsonTypeInfo<TRequest> reqTypeInfo,
        JsonTypeInfo<TResponse> resTypeInfo,
        Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}