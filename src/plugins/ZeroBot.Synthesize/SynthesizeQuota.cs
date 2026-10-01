namespace ZeroBot.Synthesize;

/// <summary>
/// 额度与日期计算工具，纯函数，便于单元测试。
/// </summary>
public static class SynthesizeQuota
{
    /// <summary>
    /// UTC+8（北京时间），中国无夏令时。
    /// </summary>
    public static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);

    /// <summary>
    /// 按 UTC+8 计算当前日期，用于限流刷新（每天 0 点重置）。
    /// </summary>
    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(now.ToOffset(ChinaOffset).DateTime);

    /// <summary>
    /// 额度 key：群聊 PeerId + 发送人 SenderId。
    /// </summary>
    public static string Key(long peerId, long senderId) => $"{peerId}:{senderId}";

    /// <summary>
    /// 解析指定群的有效每日上限：群单独配置优先，未配置时回落到默认值。
    /// </summary>
    public static int ResolveDailyLimit(IReadOnlyDictionary<long, int> groupLimits, long peerId, int defaultLimit) =>
        groupLimits.TryGetValue(peerId, out var limit) ? limit : defaultLimit;

    /// <summary>
    /// 判断在给定上限下还能否继续生成：
    /// limit &lt; 0 表示不限制；limit == 0 表示不允许；limit &gt; 0 表示需 count &lt; limit。
    /// </summary>
    public static bool CanConsume(int limit, int count) => limit < 0 || count < limit;
}
