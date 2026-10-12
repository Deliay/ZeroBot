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
    [InlineData("/小画家:公共素材")]
    public void TryParsePrompt_ShouldNotMatchOtherCommands(string raw)
    {
        Assert.False(PaintCommandHandler.TryParsePrompt(raw, out _));
    }

    [Fact]
    public void SplitMaterialReferences_ShouldStripLeadingReferences()
    {
        var (references, remaining) = PaintCommandHandler.SplitMaterialReferences("{猫}{狗}一只猫和一只狗");
        Assert.Equal("{猫}", references[0].Raw);
        Assert.Equal("猫", references[0].Name);
        Assert.Equal("{狗}", references[1].Raw);
        Assert.Equal("狗", references[1].Name);
        Assert.Equal("一只猫和一只狗", remaining);
    }

    [Fact]
    public void SplitMaterialReferences_ShouldKeepNonReferencePrompt()
    {
        var (references, remaining) = PaintCommandHandler.SplitMaterialReferences("一只猫");
        Assert.Empty(references);
        Assert.Equal("一只猫", remaining);
    }

    [Fact]
    public void SplitMaterialReferences_ShouldStopAtUnclosedBrace()
    {
        var (references, remaining) = PaintCommandHandler.SplitMaterialReferences("{猫}一只}猫");
        Assert.Single(references);
        Assert.Equal("一只}猫", remaining);
    }

    [Fact]
    public void SplitMaterialReferences_EmptyBraces_ShouldNotBeReference()
    {
        var (references, remaining) = PaintCommandHandler.SplitMaterialReferences("{}一只猫");
        Assert.Empty(references);
        Assert.Equal("{}一只猫", remaining);
    }
}
