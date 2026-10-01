using ZeroBot.Synthesize;

namespace ZeroBot.Core.Test;

public class SynthesizeQuotaTest
{
    [Fact]
    public void Today_ShouldUseChinaOffset()
    {
        var utc = new DateTimeOffset(2026, 9, 30, 16, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 1), SynthesizeQuota.Today(utc));
    }

    [Fact]
    public void Today_BeforeChinaMidnight_ShouldKeepPreviousDay()
    {
        var utc = new DateTimeOffset(2026, 9, 30, 15, 59, 59, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 9, 30), SynthesizeQuota.Today(utc));
    }

    [Fact]
    public void Key_ShouldCombinePeerAndSender()
    {
        Assert.Equal("123:456", SynthesizeQuota.Key(123, 456));
    }

    [Fact]
    public void ResolveDailyLimit_ShouldPreferGroupSetting()
    {
        var groupLimits = new Dictionary<long, int> { [111] = 7 };
        Assert.Equal(7, SynthesizeQuota.ResolveDailyLimit(groupLimits, 111, 3));
    }

    [Fact]
    public void ResolveDailyLimit_ShouldFallbackToDefaultWhenUnset()
    {
        var groupLimits = new Dictionary<long, int> { [111] = 7 };
        Assert.Equal(3, SynthesizeQuota.ResolveDailyLimit(groupLimits, 222, 3));
    }

    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(-1, 100, true)]
    [InlineData(0, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(3, 3, false)]
    public void CanConsume_ShouldRespectLimitSemantics(int limit, int count, bool expected)
    {
        Assert.Equal(expected, SynthesizeQuota.CanConsume(limit, count));
    }
}
