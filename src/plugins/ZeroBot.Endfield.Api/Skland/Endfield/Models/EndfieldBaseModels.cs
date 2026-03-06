namespace ZeroBot.Endfield.Api.Skland.Endfield.Models;

public readonly record struct MainMission(string id, string description);

public readonly record struct EndfieldCardBase(
    string serverName,
    string roleId,
    string name,
    string createTime,
    string saveTime,
    string lastLoginTime,
    int exp,
    int level,
    int worldLevel,
    int gender,
    string avatarUrl,
    MainMission mainMission,
    int charNum,
    int weaponNum,
    int docNum
);

public readonly record struct EndfieldAchieve(int count);

public readonly record struct EndfieldDungeon(string curStamina, string maxStamina, string maxTs);

public readonly record struct EndfieldBpSystem(int curLevel, int maxLevel);

public readonly record struct EndfieldDailyMission(int dailyActivation, int maxDailyActivation);

public readonly record struct EndfieldCardConfig(bool charSwitch, List<string> charIds);
