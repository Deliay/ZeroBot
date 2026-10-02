using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PaintRequestLimitsTest
{
    [Fact]
    public void TryTake_ShouldReturnRecordedLimitAndRemoveEntry()
    {
        var limits = new PaintRequestLimits();
        limits.Record(111, 222, 3);
        Assert.Equal(1, limits.Count);

        Assert.True(limits.TryTake(111, 222, out var limit));
        Assert.Equal(3, limit);

        // 取出即移除：无论消费分支是否继续（解析失败/空 prompt/额度用尽/正常处理）都不残留。
        Assert.Equal(0, limits.Count);
        Assert.False(limits.TryTake(111, 222, out _));
    }

    [Fact]
    public void TryTake_UnrecordedKey_ShouldReturnFalse()
    {
        var limits = new PaintRequestLimits();
        Assert.False(limits.TryTake(1, 2, out _));
        Assert.Equal(0, limits.Count);
    }

    [Fact]
    public void Record_ShouldOverwriteSameKey()
    {
        var limits = new PaintRequestLimits();
        limits.Record(1, 2, 1);
        limits.Record(1, 2, 5);
        Assert.Equal(1, limits.Count);
        Assert.True(limits.TryTake(1, 2, out var limit));
        Assert.Equal(5, limit);
    }

    [Fact]
    public void TryTake_ShouldDistinguishDifferentKeys()
    {
        var limits = new PaintRequestLimits();
        limits.Record(1, 2, 3);
        limits.Record(1, 3, 4);
        Assert.True(limits.TryTake(1, 3, out var limit));
        Assert.Equal(4, limit);
        Assert.Equal(1, limits.Count);
    }
}
