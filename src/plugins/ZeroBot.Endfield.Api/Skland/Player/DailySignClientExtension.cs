using System.Net.Http.Headers;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland.Player;

public static class DailySignClientExtension
{
    extension(HypergryphClient client)
    {
        public async ValueTask<DailySignV2Response> DailySignEndfieldAsync(UserCredential credential, UserAppRole role,
            CancellationToken cancellationToken = default)
        {
            if (role.appCode != "endfield") throw new InvalidOperationException("support only endfield roles");
            const string url = "https://zonai.skland.com/web/v1/game/endfield/attendance";
            var response = await client.PostCallZonAsync<DailySignV2Response>(url, "", credential, (req) =>
            {
                req.Content = new StringContent("", MediaTypeHeaderValue.Parse("application/json"));
                req.FillHeaders(new Dictionary<string, string>()
                {
                    {"sk-game-role", $"3_{role.roleId}_{role.serverId}"},
                    {"referer", "https://game.skland.com/"},
                    {"origin", "https://game.skland.com"},
                });
            }, cancellationToken);
            
            response.EnsureSuccessStatusCode();
            return response.data;
        }

        public async ValueTask<DailySignResponse> DailySignArknightsAsync(UserCredential credential, UserAppRole role,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://zonai.skland.com/api/v1/game/attendance";
            var result = await client.PostCallZonAsync<DailySignResponse>(url, new
            {
                gameId = role.gameId,
                uid = role.uid,
            }, credential, cancellationToken: cancellationToken);
            result.EnsureSuccessStatusCode();
            return result.data;
        }
    }
}
