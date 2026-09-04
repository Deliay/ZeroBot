namespace ZeroBot.Bilibili;

public record BilibiliOptions(
    Dictionary<string, HashSet<long>> RoomIdToGroupSubscriptions,
    Dictionary<string, bool> LastLiveStatus)
{
    public Dictionary<string, HashSet<long>> MidToGroupSubscriptions { get; init; } = [];
    
    public Dictionary<string, string> LastDynamicIds { get; init; } = [];
    
    public Dictionary<string, DateTimeOffset> StartLiveAt { get; init; } = [];

    public Dictionary<string, HashSet<long>> ScRoomIdToGroupSubscriptions { get; init; } = [];

    public Dictionary<string, AnchorEventSubscription> AnchorEventSubscriptions { get; init; } = [];

    public static BilibiliOptions Default => new BilibiliOptions([], []);
}

public record AnchorEventSubscription(string RoomId, string UName, HashSet<long> GroupIds);
