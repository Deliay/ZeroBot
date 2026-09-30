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
}
