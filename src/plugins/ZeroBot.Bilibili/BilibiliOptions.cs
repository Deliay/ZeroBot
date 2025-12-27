namespace ZeroBot.Bilibili;

public record BilibiliOptions(
    Dictionary<string, HashSet<long>> RoomIdToGroupSubscriptions,
    Dictionary<string, bool> LastLiveStatus)
{
    public static BilibiliOptions Default => new BilibiliOptions([], []);
}
