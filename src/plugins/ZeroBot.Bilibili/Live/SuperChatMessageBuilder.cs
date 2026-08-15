using System.Text;
using Milky.Net.Model;
using ZeroBot.Utility;

namespace ZeroBot.Bilibili.Live;

public static class SuperChatMessageBuilder
{
    public static OutgoingSegment[] Build(SuperChatData data)
    {
        var name = data.UserInfo?.Uname;
        if (string.IsNullOrWhiteSpace(name)) name = "未知用户";
        var builder = new StringBuilder();
        builder.Append(name).Append(" 的SC ¥").Append(data.Price);
        if (data.MedalInfo is { } medal && !string.IsNullOrWhiteSpace(medal.MedalName))
        {
            builder.Append(" [").Append(medal.MedalName).Append(' ').Append(medal.MedalLevel).Append(']');
        }
        builder.Append("：\n").Append(data.Message);
        return [builder.ToString().ToMilkyTextSegment()];
    }
}
