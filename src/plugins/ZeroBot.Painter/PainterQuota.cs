namespace ZeroBot.Painter;

/// <summary>
/// 每日用量与日期计算工具，纯函数，便于单元测试。
/// </summary>
public static class PainterQuota
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
    /// 用量 key：群聊 PeerId + 发送人 SenderId。
    /// </summary>
    public static string Key(long peerId, long senderId) => $"{peerId}:{senderId}";

    /// <summary>
    /// 启用指令 number 合法性：1 &lt;= number &lt;= max。
    /// </summary>
    public static bool IsValidDailyLimit(int number, int max) => number >= 1 && number <= max;
}
