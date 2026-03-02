using System.Net.Http.Json;
using ZeroBot.Endfield.Api.Skland.Login;

namespace ZeroBot.Endfield.Api.Skland;

public static class HypergryphClientExtensions
{
    extension(HypergryphClient client)
    {
        public async ValueTask<Response<T>> CallAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            var response = await client.PostAsJsonAsync(url, data, cancellationToken);
            return await response.ReadHypergryphResponseAsync<T>(cancellationToken);
        }
    }

    extension(HttpResponseMessage message)
    {
        public async ValueTask<Response<T>> ReadHypergryphResponseAsync<T>(CancellationToken cancellationToken = default)
        {
            message.EnsureSuccessStatusCode();
            return (await message.Content.ReadFromJsonAsync<Response<T>>(cancellationToken))!;
        }
    }
}