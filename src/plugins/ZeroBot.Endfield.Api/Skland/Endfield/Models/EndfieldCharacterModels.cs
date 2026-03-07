using System;
using System.Collections.Generic;
namespace ZeroBot.Endfield.Api.Skland.Endfield.Models;

public readonly record struct Rarity(string key, string value);
public readonly record struct Profession(string key, string value);
public readonly record struct Property(string key, string value);
public readonly record struct WeaponType(string key, string value);

public readonly record struct SkillType(string key, string value);
public readonly record struct SkillProperty(string key, string value);

public readonly record struct DescLevelParams(string level, Dictionary<string, string> @params);
public readonly record struct Skill(
    string id,
    string name,
    SkillType type,
    SkillProperty property,
    string iconUrl,
    string desc,
    Dictionary<string, object> descParams, // Use object for mixed types
    Dictionary<string, DescLevelParams> descLevelParams
);

public readonly record struct CharData(
    string id,
    string name,
    string avatarSqUrl,
    string avatarRtUrl,
    Rarity rarity,
    Profession profession,
    Property property,
    WeaponType weaponType,
    List<Skill> skills,
    string illustrationUrl,
    List<string> tags
);

public readonly record struct UserSkill(string skillId, int level, int maxLevel)
{
    public string GetLevelInfo()
    {
        return level switch
        {
            10 => $"精1",
            11 => $"精2",
            12 => $"精3",
            _ => $"Lv.{level}",
        };
    }
}

public readonly record struct EquipRarity(string key, string value);
public readonly record struct EquipType(string key, string value);
public readonly record struct EquipLevel(string key, string value);
public readonly record struct Suit(
    string id,
    string name,
    string skillId,
    string skillDesc,
    Dictionary<string, string> skillDescParams
);
public readonly record struct EquipData(
    string id,
    string name,
    string iconUrl,
    EquipRarity rarity,
    EquipType type,
    EquipLevel level,
    List<string> properties,
    bool isAccessory,
    Suit? suit,
    string function,
    string pkg
);
public readonly record struct Equip(string equipId, EquipData equipData);

public readonly record struct TacticalItemRarity(string key, string value);
public readonly record struct ActiveEffectType(string key, string value);
public readonly record struct TacticalItemData(
    string id,
    string name,
    string iconUrl,
    TacticalItemRarity rarity,
    ActiveEffectType activeEffectType,
    string activeEffect,
    string passiveEffect,
    Dictionary<string, string> activeEffectParams,
    Dictionary<string, string> passiveEffectParams
);
public readonly record struct TacticalItem(string tacticalItemId, TacticalItemData tacticalItemData);

public readonly record struct WeaponRarity(string key, string value);
public readonly record struct WeaponDataType(string key, string value);
public readonly record struct WeaponSkill(string key, string value);
public readonly record struct WeaponData(
    string id,
    string name,
    string iconUrl,
    WeaponRarity rarity,
    WeaponDataType type,
    string function,
    string description,
    List<WeaponSkill> skills
);
public readonly record struct Gem(string id, string icon);
public readonly record struct Weapon(
    WeaponData weaponData,
    int level,
    int refineLevel,
    int breakthroughLevel,
    Gem? gem
);

public readonly record struct EndfieldCharacter(
    CharData charData,
    string id,
    int level,
    Dictionary<string, UserSkill> userSkills,
    Equip bodyEquip,
    Equip armEquip,
    Equip firstAccessory,
    Equip secondAccessory,
    TacticalItem tacticalItem,
    int evolvePhase,
    int potentialLevel,
    Weapon weapon,
    string gender,
    string ownTs
)
{
    public string GetEquipmentSetInfo()
    {
        return new List<string?>([
            bodyEquip.equipData.suit?.name,
            armEquip.equipData.suit?.name,
            firstAccessory.equipData.suit?.name,
            secondAccessory.equipData.suit?.name,])
            .Where(x => x is not null)
            .GroupBy(x => x)
            .Select(x => (Suit: x.Key!, Count: x.Count()))
            .OrderByDescending(x => x.Count)
            .Select(x => $"{x.Suit}*{x.Count}")
            .Aggregate((a, b) => $"{a} + {b}");
    }

    public string ToCharacterInfo()
    {
        var nameLevel = $"{charData.name}(Lv.{level})";
        var charWeaponPotential = $"[{potentialLevel}+{weapon.refineLevel + 1} {weapon.weaponData.name}({weapon.level})]";
        return $"{nameLevel} {charWeaponPotential} {GetEquipmentSetInfo()}";
    }
}
