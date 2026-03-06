using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland;

public readonly record struct SklandUser(string id, string nickname, string hgId);

public readonly record struct SklandUserResponse(SklandUser user);

public static class UserApiExtensions
{
    extension(HypergryphClient client)
    {
        public async ValueTask<SklandUser> GetCurrentUserAsync(UserCredential credential,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://zonai.skland.com/api/v1/user";
            var res = await client.GetCallZonAsync<SklandUserResponse>(url, credential, cancellationToken);
            res.EnsureSuccessStatusCode();
            return res.data.user;
        }
    }
}