using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Endfield;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Utility;

namespace ZeroBot.Endfield.Component;

public class EndfieldCommandHandlers(
    HypergryphClient client,
    CredentialManager credentialManager,
    IBotContext bot,
    IMemoryCache cache)
{
    private readonly MemoryCacheEntryOptions _defaultExpireOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(7)
    };

    private async IAsyncEnumerable<string> GenerateInfoAsync(string userId, 
        [EnumeratorCancellation]CancellationToken cancellationToken)
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
                    .Flat().Where(role => role.appCode == "endfield"),
                _defaultExpireOptions) ?? []);
    
            foreach (var role in roles)
            {
                var info = await cache.GetOrCreateAsync($"{userId}-{role.roleId}",
                    async _ => await client.GetEndfieldInfoAsync(role, credential, cancellationToken),
                    _defaultExpireOptions);

                yield return $"{info.@base.name}(UID:{role.roleId}) Lv.{info.@base.level} (世界等级 {info.@base.worldLevel})\n" +
                             $"理智:{info.dungeon.curStamina}/{info.dungeon.maxStamina}(回满: {DateTimeOffset.FromUnixTimeSeconds(long.Parse(info.dungeon.maxTs)):yyyy-MM-dd hh:mm:ss})\n" +
                             $"日常进度:{info.dailyMission.dailyActivation}/{info.dailyMission.maxDailyActivation}, 通行证进度:{info.bpSystem.curLevel}/{info.bpSystem.maxLevel}\n" +
                             $"主线进度:{info.@base.mainMission.description}";
            }
        } 
    }
    
    public async ValueTask MyEndfieldInfoAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var userId = $"{message.Data.SenderId}";
        var msgList = await GenerateInfoAsync(userId, cancellationToken)
            .ToListAsync(cancellationToken);

        if (msgList.Count == 0)
        {
            await message.Reply(bot, cancellationToken,
                ["没有找到可以查询的终末地角色，请使用「/鹰角:绑定」进行绑定（使用森空岛App扫码）".ToMilkyTextSegment()]);
            return;
        }
        
        var msg = string.Join("\n\n", msgList);
        
        await message.Reply(bot, cancellationToken, [msg.ToMilkyTextSegment()]);
    }
}