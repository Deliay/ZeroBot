using System.Collections.Concurrent;

namespace ZeroBot.TestPlugin.Config;

public record BootsTest(
    long messageId,
    DateTimeOffset questedAt,
    bool isResolved = false,
    DateTimeOffset resolvedAt = default);


public record GroupBoots(
    long groupId,
    string? currentQuestion,
    Dictionary<string, BootsTest> questionRecords)
{
    public static GroupBoots Create(long groupId) => new(groupId, null, []);
}


public record BootsConfig(Dictionary<long, GroupBoots> groupBoots, string questionDir = "boots")
{
    public static BootsConfig Default => new([]);
}
