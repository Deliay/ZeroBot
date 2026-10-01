using ZeroBot.Synthesize;

namespace ZeroBot.Core.Test;

public class VoiceBroadcastServiceTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void PrepareText_BlankInput_ReturnsNull(string? input)
    {
        Assert.Null(VoiceBroadcastService.PrepareText(input, 200));
    }

    [Fact]
    public void PrepareText_TrimsWhitespace()
    {
        Assert.Equal("你好", VoiceBroadcastService.PrepareText("  你好  ", 200));
    }

    [Fact]
    public void PrepareText_TruncatesToMaxLength()
    {
        var text = new string('a', 250);
        var result = VoiceBroadcastService.PrepareText(text, 200);
        Assert.NotNull(result);
        Assert.Equal(200, result.Length);
        Assert.Equal(new string('a', 200), result);
    }

    [Fact]
    public void PrepareText_MaxLengthNotPositive_DoesNotTruncate()
    {
        var text = new string('a', 250);
        Assert.Equal(text, VoiceBroadcastService.PrepareText(text, 0));
        Assert.Equal(text, VoiceBroadcastService.PrepareText(text, -5));
    }

    [Fact]
    public void PrepareText_LengthExactlyMaxLength_NotTruncated()
    {
        Assert.Equal("abc", VoiceBroadcastService.PrepareText("abc", 3));
    }
}
