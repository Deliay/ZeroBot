using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;

namespace ZeroBot.Endfield.Service;

public class SklandService(
    HypergryphClient client,
    CredentialManager credentialManager,
    IMemoryCache cache)
{
    private readonly MemoryCacheEntryOptions _defaultExpireOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(7)
    };
    public async IAsyncEnumerable<(UserCredential, UserAppRole)> EnumerateUserRolesAsync(string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await credentialManager.RenewalRefreshTokenAsync(userId, cancellationToken);
        var allCredentials = await credentialManager.GetCurrentCredentialAsync(userId, cancellationToken);
        foreach (var credential in allCredentials)
        {
            credential.SklandUserId ??= await cache.GetOrCreateAsync($"{userId}-{credential.OAuthToken}-sk-user", async _ =>
            {
                if (credential.SklandUserId is not null) return credential.SklandUserId;

                var skUser = await client.GetCurrentUserAsync(credential, cancellationToken);
                return skUser.id;
            }, _defaultExpireOptions);
            
            var roles = (await cache.GetOrCreateAsync($"{userId}-{credential.OAuthToken}-bindings",
                async _ => (await client.GetPlayerBindings(credential, cancellationToken))
                    .Flat(),
                _defaultExpireOptions) ?? []);
            
            foreach (var userAppRole in roles)
            {
                yield return (credential, userAppRole);
            }
        }
    } 
}