using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Player;

public readonly record struct UserAppRole(
    string appCode,
    int gameId,
    string uid,
    string channelName,
    string gameName,
    string nickName,
    string nickname,
    string roleId,
    string serverId);

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
