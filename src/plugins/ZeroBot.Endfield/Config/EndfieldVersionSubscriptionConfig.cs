namespace ZeroBot.Endfield.Config;

public record EndfieldVersionSubscriptionConfig(
    HashSet<long> SubscribedGroupIds,
    string? LastKnownVersion)
{
    public static EndfieldVersionSubscriptionConfig Empty => new([], null);
}
