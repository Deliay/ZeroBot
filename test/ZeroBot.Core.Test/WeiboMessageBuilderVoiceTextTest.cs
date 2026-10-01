using ZeroBot.Weibo.Weibo;

namespace ZeroBot.Core.Test;

public class WeiboMessageBuilderVoiceTextTest
{
    [Fact]
    public void BuildVoiceText_NormalWeibo_ReturnsPlainText()
    {
        var item = Item(new WeiboStatus
        {
            Text = "你好<br/>世界<a href=\"https://x\">链接</a><img src='x'>&amp;"
        });

        var result = WeiboMessageBuilder.BuildVoiceText(item);
        Assert.Equal("你好\n世界链接&", result);
        Assert.DoesNotContain("https://x", result);
        Assert.DoesNotContain("<", result);
    }

    [Fact]
    public void BuildVoiceText_ForwardWeibo_ConcatenatesForwarderAndOriginal()
    {
        var item = Item(new WeiboStatus
        {
            Text = "转发理由//@某人: 原始微博内容",
            RetweetedStatus = new WeiboStatus
            {
                Text = "原博正文",
                User = new WeiboUser { ScreenName = "作者" }
            }
        });

        var result = WeiboMessageBuilder.BuildVoiceText(item);
        Assert.Equal("转发理由\n@作者: 原博正文", result);
        Assert.DoesNotContain("//@", result);
    }

    [Fact]
    public void BuildVoiceText_OriginalDeleted_ReturnsForwarderTextOnly()
    {
        var item = Item(new WeiboStatus
        {
            Text = "转发理由",
            RetweetedStatus = new WeiboStatus
            {
                Text = "该微博已被删除",
                User = new WeiboUser { ScreenName = "作者" }
            }
        });

        Assert.Equal("转发理由", WeiboMessageBuilder.BuildVoiceText(item));
    }

    [Fact]
    public void BuildVoiceText_OriginalUserMissing_ReturnsForwarderTextOnly()
    {
        var item = Item(new WeiboStatus
        {
            Text = "转发理由",
            RetweetedStatus = new WeiboStatus
            {
                Text = "原博正文",
                User = null
            }
        });

        Assert.Equal("转发理由", WeiboMessageBuilder.BuildVoiceText(item));
    }

    [Fact]
    public void BuildVoiceText_DataNull_ReturnsEmpty()
    {
        Assert.Equal("", WeiboMessageBuilder.BuildVoiceText(new WeiboTimelineItem()));
    }

    [Fact]
    public void BuildVoiceText_PureImageWeibo_ReturnsEmpty()
    {
        var item = Item(new WeiboStatus
        {
            Text = "<img src='https://example.com/a.jpg'>",
            Pics = [new WeiboPic { Url = "https://example.com/a.jpg" }]
        });

        Assert.Equal("", WeiboMessageBuilder.BuildVoiceText(item));
    }

    private static WeiboTimelineItem Item(WeiboStatus status) => new() { Data = status };
}
