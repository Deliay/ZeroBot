using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Player;

public readonly record struct UserAppRole(
    string appCode,
    int gameId,
    string uid,
    string channelName,
    string gameName,
    string nickName,
    string roleNickname,
    string roleId,
    string serverId)
{
    public override string ToString()
    {
        return $"{channelName}/{gameName} {roleNickname}(UID:{roleId})";
    }
}

public readonly record struct UserRole(
    string nickname,
    string roleId,
    string serverId);

public readonly record struct UserAppBinding(
    List<UserRole> roles,
    int gameId = 1,
    string uid = "",
    string channelName = "Unknown",
    string gameName = "Unknown",
    string nickName = "Unknown");

public readonly record struct UserAppBindings(string appCode, List<UserAppBinding> bindingList);

public readonly record struct UserAllBindings(List<UserAppBindings> list)
{
    public IEnumerable<UserAppRole> Flat()
    {
        return from userAppBindings in list
            from userAppBinding in userAppBindings.bindingList
            from userRole in userAppBinding.roles
            select new UserAppRole(userAppBindings.appCode, userAppBinding.gameId, userAppBinding.uid,
            userAppBinding.channelName, userAppBinding.gameName, userAppBinding.nickName,
            userRole.nickname, userRole.roleId, userRole.serverId);
    }
}

public readonly record struct DailySignResource(string name);

public readonly record struct DailySignReward(DailySignResource resource, int count)
{
    public override string ToString()
    {
        return $"{resource.name} * {count}";
    }
}

public readonly record struct DailySignResponse(List<DailySignReward> awards)
{
    public override string ToString()
    {
        return string.Join('\n', awards.Select(x => x.ToString()));
    }
}

public readonly record struct DailySignV2Resource(string name, int count);
public readonly record struct DailySignV2Reward(string id);
public readonly record struct DailySignV2Response(
    List<DailySignV2Reward> awardIds,
    Dictionary<string, DailySignV2Resource> resourceInfoMap);
