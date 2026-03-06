using System;
using System.Collections.Generic;
namespace ZeroBot.Endfield.Api.Skland.Endfield.Models;
public readonly record struct SpaceShipCharacter(string charId, float physicalStrength, int favorability);

public readonly record struct ReportEntry(List<string> @char, Dictionary<string, int> output, string createdTimeTs);

public readonly record struct Room(
    string id,
    int type,
    int level,
    List<SpaceShipCharacter> chars,
    Dictionary<string, ReportEntry> reports
);

public readonly record struct EndfieldSpaceshipData(List<Room> rooms);
