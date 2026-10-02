using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PainterQuotaTest
{
    [Fact]
    public void Today_ShouldUseChinaOffset()
    {
        var utc = new DateTimeOffset(2026, 9, 30, 16, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 1), PainterQuota.Today(utc));
    }

    [Fact]
    public void Today_BeforeChinaMidnight_ShouldKeepPreviousDay()
    {
        var utc = new DateTimeOffset(2026, 9, 30, 15, 59, 59, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 9, 30), PainterQuota.Today(utc));
    }

    [Fact]
    public void Key_ShouldCombinePeerAndSender()
    {
        Assert.Equal("123:456", PainterQuota.Key(123, 456));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(-1, true)]
    public void IsValidDailyLimit_ShouldRespectBoundaries(int number, bool expected)
    {
        Assert.Equal(expected, PainterQuota.IsValidDailyLimit(number, 5));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void IsValidDailyLimit_ShouldRespectConfiguredMax(int number, bool expected)
    {
        Assert.Equal(expected, PainterQuota.IsValidDailyLimit(number, 1));
    }
}
