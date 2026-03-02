using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland.Player;

public static class PlayerClientExtension
{
    extension(HypergryphClient client)
    {
        public async ValueTask<UserAllBindings> GetPlayerBindings(Credential credential,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://zonai.skland.com/api/v1/game/player/binding";
            var result = await client.GetCallAsync<UserAllBindings>(
                url, credential, cancellationToken);

            result.EnsureSuccessStatusCode();
            return result.data;
        }
    }
}