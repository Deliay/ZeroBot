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
                    { "sk-game-role", $"3_{role.roleId}_{role.serverId}" },
                    { "referer", "https://game.skland.com/" },
                    { "origin", "https://game.skland.com" },
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

        public async ValueTask<DailySignResponse> DailySignAsync(UserCredential credential, UserAppRole role,
            CancellationToken cancellationToken = default)
        {
            switch (role.appCode)
            {
                case "endfield":
                {
                    var result = await client.DailySignEndfieldAsync(credential, role, cancellationToken);
                
                    return new DailySignResponse(result.awardIds
                        .Where(award => result.resourceInfoMap.ContainsKey(award.id))
                        .Select(award =>
                        {
                            var resource = result.resourceInfoMap[award.id];
                            return new DailySignReward(new DailySignResource(resource.name), resource.count);
                        }).ToList());
                }
                case "arknights":
                    return await client.DailySignArknightsAsync(credential, role, cancellationToken);
                default:
                    throw new InvalidOperationException("support only arknights & endfield");
            }
        }
    }

    extension(UserAppRole role)
    {
        public bool IsSupportSign => role.appCode is "endfield" or "arknights";
    }
}