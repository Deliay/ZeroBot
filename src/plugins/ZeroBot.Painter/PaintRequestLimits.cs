using System.Collections.Concurrent;

namespace ZeroBot.Painter;

/// <summary>
/// 「发起时群上限」暂存表，key = (PeerId, MessageSeq)。
/// 入队前 <see cref="Record"/> 记录，<c>DequeueAsync</c> 入口 <see cref="TryTake"/> 取出即移除，
/// 保证解析失败 / 空 prompt / 额度用尽 / 正常处理等任何分支都不残留记录（避免长驻进程内存泄漏）。
/// </summary>
public sealed class PaintRequestLimits
{
    private readonly ConcurrentDictionary<(long PeerId, long MessageSeq), int> _limits = new();

    /// <summary>当前未消费的记录数，用于测试与诊断。</summary>
    public int Count => _limits.Count;

    /// <summary>入队前记录本请求发起时的群上限。</summary>
    public void Record(long peerId, long messageSeq, int limit) =>
        _limits[(peerId, messageSeq)] = limit;

    /// <summary>取出并移除发起时群上限；不存在返回 false。</summary>
    public bool TryTake(long peerId, long messageSeq, out int limit) =>
        _limits.TryRemove((peerId, messageSeq), out limit);
}
