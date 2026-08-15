namespace ZeroBot.Bilibili;

public record BilibiliOptions(
    Dictionary<string, HashSet<long>> RoomIdToGroupSubscriptions,
    Dictionary<string, bool> LastLiveStatus)
{
    public Dictionary<string, HashSet<long>> MidToGroupSubscriptions { get; init; } = [];

    public Dictionary<string, string> LastDynamicIds { get; init; } = [];

    public static BilibiliOptions Default => new BilibiliOptions([], []);
}
