using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland.Player;

public static class PlayerClientExtension
{
    extension(HypergryphClient client)
    {
        public async ValueTask<UserAllBindings> GetPlayerBindings(UserCredential userCredential,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://zonai.skland.com/api/v1/game/player/binding";
            var result = await client.GetCallZonAsync<UserAllBindings>(
                url, userCredential, cancellationToken);

            result.EnsureSuccessStatusCode();
            return result.data;
        }
    }
}