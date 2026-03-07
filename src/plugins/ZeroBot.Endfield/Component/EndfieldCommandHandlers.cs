using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Endfield;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Endfield.Service;
using ZeroBot.Utility;

namespace ZeroBot.Endfield.Component;

public class EndfieldCommandHandlers(
    HypergryphClient client,
    CredentialManager credentialManager,
    IBotContext bot,
    SklandService skland,
    IMemoryCache cache)
{
    private readonly MemoryCacheEntryOptions _defaultExpireOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(7)
    };

    private async IAsyncEnumerable<string> GenerateCharInfoAsync(string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var (credential, role) in skland.EnumerateUserRolesAsync(userId, cancellationToken))
        {
            var info = await cache.GetOrCreateAsync($"{userId}-{role.roleId}",
                async _ => await client.GetEndfieldInfoAsync(role, credential, cancellationToken),
                _defaultExpireOptions);
            
            yield return $"=== {info.@base.name}[UID:{role.roleId}] >=60级的角色 ===";
            foreach (var character in info.chars.Where(c => c.level >= 60))
            {
                yield return character.ToCharacterInfo();
            }
        }
    }
    
    private async IAsyncEnumerable<string> GenerateInfoAsync(string userId, 
        [EnumeratorCancellation]CancellationToken cancellationToken)
    {
        await foreach (var (credential, role) in skland.EnumerateUserRolesAsync(userId, cancellationToken))
        {
            var info = await cache.GetOrCreateAsync($"{userId}-{role.roleId}",
                async _ => await client.GetEndfieldInfoAsync(role, credential, cancellationToken),
                _defaultExpireOptions);

            var dailyMission = info.dailyMission.dailyActivation == info.dailyMission.maxDailyActivation
                ? "已完成"
                : $" {info.dailyMission.dailyActivation}/{info.dailyMission.maxDailyActivation}";

            var bpSystem = info.bpSystem.curLevel == info.bpSystem.maxLevel
                ? "已满级"
                : $" {info.bpSystem.curLevel}/{info.bpSystem.maxLevel}";
            var dungeonMaxAt =
                $"{DateTimeOffset.FromUnixTimeSeconds(long.Parse(info.dungeon.maxTs)).AddHours(8):yyyy-MM-dd hh:mm:ss}";
            yield return $"{info.@base.name}[UID:{role.roleId}][Lv.{info.@base.level}][世界等级 {info.@base.worldLevel}])\n" +
                         $"理智 {info.dungeon.curStamina}/{info.dungeon.maxStamina}({dungeonMaxAt}回满)\n" +
                         $"日常{dailyMission}, 通行证{bpSystem}\n" +
                         $"主线进度:{info.@base.mainMission.description}";
        }
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
                ["没有找到可以查询的终末地角色，请私聊我使用「/鹰角:绑定」进行绑定（使用森空岛App扫码）".ToMilkyTextSegment()]);
            return;
        }
        
        var msg = string.Join("\n\n", msgList);
        
        await message.Reply(bot, cancellationToken, [msg.ToMilkyTextSegment()]);
    }

    public async ValueTask MyEndfieldCharacterInfoAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var userId = $"{message.Data.SenderId}";
        var infoStrList = await GenerateCharInfoAsync(userId, cancellationToken)
            .ToListAsync(cancellationToken);

        if (infoStrList.Count == 0)
        {
            await message.Reply(bot, cancellationToken,
                ["没有找到可以查询的终末地角色，请私聊我使用「/鹰角:绑定」进行绑定（使用森空岛App扫码）".ToMilkyTextSegment()]);
            return;
        }
        
        var msg = string.Join("\n", infoStrList);
        
        await message.Reply(bot, cancellationToken, [msg.ToMilkyTextSegment()]);
    }
}