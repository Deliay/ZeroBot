using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PainterMaterialCommandHandlerTest
{
    [Fact]
    public void TryParse_ListWithoutAction()
    {
        Assert.True(PainterMaterialCommandHandler.TryParse("/小画家:公共素材", out var action, out var name));
        Assert.Equal(string.Empty, action);
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void TryParse_Add()
    {
        Assert.True(PainterMaterialCommandHandler.TryParse("/小画家:公共素材:添加:猫", out var action, out var name));
        Assert.Equal("添加", action);
        Assert.Equal("猫", name);
    }

    [Fact]
    public void TryParse_DeleteWithFullWidthSeparator()
    {
        Assert.True(PainterMaterialCommandHandler.TryParse("/小画家：公共素材：删除：猫", out var action, out var name));
        Assert.Equal("删除", action);
        Assert.Equal("猫", name);
    }

    [Fact]
    public void TryParse_NameShouldPreserveSpacesAndHyphen()
    {
        Assert.True(PainterMaterialCommandHandler.TryParse("/小画家:公共素材:添加:my cat-1", out _, out var name));
        Assert.Equal("my cat-1", name);
    }

    [Fact]
    public void TryParse_AddWithoutName()
    {
        Assert.True(PainterMaterialCommandHandler.TryParse("/小画家:公共素材:添加", out var action, out var name));
        Assert.Equal("添加", action);
        Assert.Equal(string.Empty, name);
    }

    [Theory]
    [InlineData("/小画家:公共素材册:x")]
    [InlineData("/小画家:画:一只猫")]
    [InlineData("/小画家:启用:3")]
    [InlineData("/小画家：画")]
    public void TryParse_ShouldNotMatchOtherCommands(string raw)
    {
        Assert.False(PainterMaterialCommandHandler.TryParse(raw, out _, out _));
    }
}
