using Microsoft.Extensions.Caching.Memory;
using Milky.Net.Client;
using Milky.Net.Model;

namespace ZeroBot.Milky.Bot;

public class BotInfos(MilkyClient milky, IMemoryCache cache)
{
    public async ValueTask<GetLoginInfoOutput> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await cache.GetOrCreateAsync("account", _ => milky.System.GetLoginInfoAsync(cancellationToken));
        return result!;
    }

    private readonly MemoryCacheEntryOptions _defaultExpireOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
    
    public async ValueTask<GetGroupInfoOutput> GetGroupInfoAsync(long groupId,
        CancellationToken cancellationToken = default)
    {
        var result = await cache.GetOrCreateAsync($"group-{groupId}",
            _ => milky.System.GetGroupInfoAsync(new GetGroupInfoInput(groupId, true), cancellationToken),
            _defaultExpireOptions);

        return result!;
    }
    public async ValueTask<Dictionary<long, GroupEntity>> GetGroupListAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await cache.GetOrCreateAsync($"group-list", async _ =>
            {
                var groups = await milky.System.GetGroupListAsync(new GetGroupListInput(true), cancellationToken);
                return groups.Groups.ToDictionary(g => g.GroupId);
            },
            _defaultExpireOptions);

        return result!;
    }
}