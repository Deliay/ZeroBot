namespace ZeroBot.Endfield.Api.Skland.Endfield.Models;

public readonly record struct EndfieldCardDetail(
    EndfieldCardBase @base,
    List<EndfieldCharacter> chars,
    EndfieldAchieve achieve,
    EndfieldSpaceshipData spaceShip,
    List<EndfieldDomainData> domain,
    EndfieldDungeon dungeon,
    EndfieldBpSystem bpSystem,
    EndfieldDailyMission dailyMission,
    EndfieldCardConfig config,
    string currentTs);
    
public readonly record struct EndfieldCardResponse(EndfieldCardDetail detail);
