using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;

namespace ZeroBot.Endfield.Api.Skland.Arknights;

public static class InfoApiExtensions
{
    extension(HypergryphClient client)
    {
        public async ValueTask<JsonDocument> GetArknightsInfoAsync(string uid, UserCredential userCredential,
            CancellationToken cancellationToken = default)
        {
            var url = $"https://zonai.skland.com/api/v1/game/player/info?uid={uid}";
            var result = await client.GetCallZonAsync<JsonDocument>(url, userCredential, cancellationToken);
            result.EnsureSuccessStatusCode();
            return result.data;
        }
    }
}