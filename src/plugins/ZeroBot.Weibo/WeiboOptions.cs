namespace ZeroBot.Weibo;

public record WeiboOptions
{
    public static WeiboOptions Default => new();
    public Dictionary<string, HashSet<long>> UidToGroupSubscriptions { get; init; } = [];
    public Dictionary<string, string> LastWeiboIds { get; init; } = [];  // uid -> mblogId
}