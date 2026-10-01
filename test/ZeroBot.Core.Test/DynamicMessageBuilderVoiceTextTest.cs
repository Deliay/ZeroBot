using ZeroBot.Bilibili.Dynamic;

namespace ZeroBot.Core.Test;

public class DynamicMessageBuilderVoiceTextTest
{
    [Fact]
    public void BuildVoiceText_WithTitleAndSummary_ReturnsTitleThenBody()
    {
        var data = Dynamic("DYNAMIC_TYPE_AV", "标题", "正文内容");
        Assert.Equal("标题\n正文内容", DynamicMessageBuilder.BuildVoiceText(data));
    }

    [Fact]
    public void BuildVoiceText_WithoutTitle_ReturnsBodyOnly()
    {
        var data = Dynamic("DYNAMIC_TYPE_AV", null, "正文内容");
        Assert.Equal("正文内容", DynamicMessageBuilder.BuildVoiceText(data));
    }

    [Fact]
    public void BuildVoiceText_PureImageDynamic_ReturnsEmpty()
    {
        var data = new DynamicData
        {
            Type = "DYNAMIC_TYPE_DRAW",
            Modules = new DynamicModules
            {
                ModuleDynamic = new ModuleDynamic
                {
                    Major = new DynamicMajor
                    {
                        Opus = new DynamicOpus
                        {
                            Title = null,
                            Summary = new DynamicRichText { Text = "" },
                            Pics = [new DynamicPic { Url = "https://example.com/a.jpg" }],
                            JumpUrl = "https://www.bilibili.com/opus/1"
                        }
                    }
                }
            }
        };

        Assert.Equal("", DynamicMessageBuilder.BuildVoiceText(data));
    }

    [Fact]
    public void BuildVoiceText_ForwardDynamic_ConcatenatesForwarderAndOrig()
    {
        var orig = Dynamic("DYNAMIC_TYPE_AV", "原标题", "原正文");
        var forward = new DynamicData
        {
            Type = "DYNAMIC_TYPE_FORWARD",
            Orig = orig,
            Modules = new DynamicModules
            {
                ModuleDynamic = new ModuleDynamic
                {
                    Desc = new DynamicRichText { Text = "转发的话" }
                }
            }
        };

        Assert.Equal("转发的话\n原标题\n原正文", DynamicMessageBuilder.BuildVoiceText(forward));
    }

    [Fact]
    public void BuildVoiceText_WithoutOpus_FallsBackToDesc()
    {
        var data = new DynamicData
        {
            Type = "DYNAMIC_TYPE_WORD",
            Modules = new DynamicModules
            {
                ModuleDynamic = new ModuleDynamic
                {
                    Desc = new DynamicRichText { Text = "只有描述" }
                }
            }
        };

        Assert.Equal("只有描述", DynamicMessageBuilder.BuildVoiceText(data));
    }

    [Fact]
    public void BuildVoiceText_NoText_ReturnsEmpty()
    {
        var data = new DynamicData { Type = "DYNAMIC_TYPE_DRAW" };
        Assert.Equal("", DynamicMessageBuilder.BuildVoiceText(data));
    }

    private static DynamicData Dynamic(string type, string? title, string summary)
    {
        return new DynamicData
        {
            Type = type,
            Modules = new DynamicModules
            {
                ModuleDynamic = new ModuleDynamic
                {
                    Major = new DynamicMajor
                    {
                        Opus = new DynamicOpus
                        {
                            Title = title,
                            Summary = new DynamicRichText { Text = summary }
                        }
                    }
                }
            }
        };
    }
}
