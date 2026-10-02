using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PaintCommandHandlerTest
{
    [Fact]
    public void TryParsePrompt_ShouldExtractPrompt()
    {
        Assert.True(PaintCommandHandler.TryParsePrompt("/小画家:画:一只在樱花树下打盹的猫", out var prompt));
        Assert.Equal("一只在樱花树下打盹的猫", prompt);
    }

    [Fact]
    public void TryParsePrompt_ShouldPreserveColonHyphenAndSpace()
    {
        Assert.True(PaintCommandHandler.TryParsePrompt("/小画家:画:a:b-c d", out var prompt));
        Assert.Equal("a:b-c d", prompt);
    }

    [Fact]
    public void TryParsePrompt_ShouldSupportFullWidthSeparator()
    {
        Assert.True(PaintCommandHandler.TryParsePrompt("/小画家：画：一只猫", out var prompt));
        Assert.Equal("一只猫", prompt);
    }

    [Fact]
    public void TryParsePrompt_WithoutPrompt_ShouldMatchWithEmptyPrompt()
    {
        Assert.True(PaintCommandHandler.TryParsePrompt("/小画家:画", out var prompt));
        Assert.Equal(string.Empty, prompt);

        Assert.True(PaintCommandHandler.TryParsePrompt("/小画家:画:", out var promptAfterSeparator));
        Assert.Equal(string.Empty, promptAfterSeparator);
    }

    [Theory]
    [InlineData("/小画家:画册:x")]
    [InlineData("/变毬")]
    [InlineData("/小画家:启用:3")]
    [InlineData("/小画家:禁用")]
    public void TryParsePrompt_ShouldNotMatchOtherCommands(string raw)
    {
        Assert.False(PaintCommandHandler.TryParsePrompt(raw, out _));
    }
}
