namespace ZeroBot.Endfield.Config;

public record EndfieldServerStatusSubscriptionConfig(
    HashSet<long> SubscribedGroupIds,
    bool? LastKnownStatus)
{
    public static EndfieldServerStatusSubscriptionConfig Empty => new([], null);
}