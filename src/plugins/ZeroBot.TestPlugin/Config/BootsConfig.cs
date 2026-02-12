using System.Collections.Concurrent;

namespace ZeroBot.TestPlugin.Config;

public record BootsTest(
    long messageId,
    DateTimeOffset questedAt,
    bool isResolved = false,
    DateTimeOffset resolvedAt = default,
    long resolvedBy = 0);


public record GroupBoots(
    long groupId,
    string? currentQuestion,
    Dictionary<string, BootsTest> questionRecords,
    Dictionary<long, List<string>>? questRecords)
{
    public static GroupBoots Create(long groupId) => new(groupId, null, [], []);
}


public record BootsConfig(Dictionary<long, GroupBoots> groupBoots, string questionDir = "boots")
{
    public static BootsConfig Default => new([]);
}
