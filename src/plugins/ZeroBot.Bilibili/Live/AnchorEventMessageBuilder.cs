using System.Text;
using Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand;
using Milky.Net.Model;
using ZeroBot.Utility;

namespace ZeroBot.Bilibili.Live;

public static class AnchorEventMessageBuilder
{
    public static OutgoingSegment[] BuildDanmaku(string anchorName, DanmuMsg msg)
    {
        var builder = new StringBuilder();
        builder.Append(anchorName).Append(" 的弹幕");
        if (!string.IsNullOrWhiteSpace(msg.FansTag))
        {
            builder.Append(" [").Append(msg.FansTag).Append(' ').Append(msg.FansLevel).Append(']');
        }
        builder.Append("：\n").Append(msg.Msg);
        var segments = new List<OutgoingSegment> { builder.ToString().ToMilkyTextSegment() };
        if (!string.IsNullOrWhiteSpace(msg.MemeUrl))
        {
            segments.Add(msg.MemeUrl.ToMilkyImageSegment());
        }
        return segments.ToArray();
    }

    public static OutgoingSegment[] BuildEnter(string anchorName)
    {
        return [$"{anchorName} 进入了直播间！".ToMilkyTextSegment()];
    }
}
