using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Endfield.Models;
using ZeroBot.Endfield.Api.Skland.Player;

namespace ZeroBot.Endfield.Api.Skland.Endfield;

public static class CardApiExtensions
{
    extension(HypergryphClient client)
    {
        public async ValueTask<EndfieldCardDetail> GetEndfieldInfoAsync(UserAppRole role, UserCredential credential,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://zonai.skland.com/api/v1/game/endfield/card/detail";
            var param = $"roleId={role.roleId}&serverId={role.serverId}&userId={credential.SklandUserId}";

            var response = await client.GetCallZonAsync<EndfieldCardResponse>($"{url}?{param}", credential, cancellationToken);
            response.EnsureSuccessStatusCode();

            return response.data.detail;
        }
    }
}
