using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Milky.Net.Model;
using ZeroBot.Utility;

namespace ZeroBot.Weibo.Weibo;

public static class WeiboMessageBuilder
{
    public static OutgoingSegment[] Build(WeiboTimelineItem item)
    {
        var data = item.Data;
        if (data == null)
            return ["[微博] 无法获取微博内容".ToMilkyTextSegment()];

        var author = data.User?.ScreenName ?? "神秘人";
        var segments = new List<OutgoingSegment>
        {
            $"{author} 发布了新微博\n".ToMilkyTextSegment(),
        };

        AppendWeibo(data, item.MblogId, segments);

        // append original link
        segments.Add($"\nhttps://m.weibo.cn/status/{item.MblogId}".ToMilkyTextSegment());

        return [.. segments];
    }

    private static void AppendWeibo(WeiboStatus data, string mblogId, List<OutgoingSegment> segments, bool isOrig = false)
    {
        var text = new StringBuilder();

        // forward weibo
        if (data.RetweetedStatus != null)
        {
            // render forward text
            var forwardText = HtmlToPlainText(data.Text ?? "");
            // cut the //@... tail
            var atIndex = forwardText.LastIndexOf("//@", StringComparison.Ordinal);
            if (atIndex > 0)
                forwardText = forwardText[..atIndex];
            text.Append(forwardText.Trim());

            // render retweeted status
            var origAuthor = data.RetweetedStatus.User?.ScreenName;
            var origText = HtmlToPlainText(data.RetweetedStatus.Text ?? "");
            text.Append('\n').Append("---- 转发 ----\n");

            if (data.RetweetedStatus.User == null || string.IsNullOrEmpty(origText) || origText.Contains("微博已被删除"))
            {
                text.Append("[原微博不可见]");
            }
            else
            {
                text.Append($"@{origAuthor}: {origText}");
            }

            segments.Add(text.ToString().ToMilkyTextSegment());

            // add pics from retweeted status if any
            if (data.RetweetedStatus.Pics is { Count: > 0 })
            {
                foreach (var pic in data.RetweetedStatus.Pics)
                {
                    var picUrl = GetLargePicUrl(pic);
                    if (picUrl != null)
                        segments.Add(picUrl.ToMilkyImageSegment());
                }
            }
            return;
        }

        // normal weibo
        text.Append(HtmlToPlainText(data.Text ?? ""));

        // add pics
        if (data.Pics is { Count: > 0 })
        {
            text.Append('\n');
            segments.Add(text.ToString().ToMilkyTextSegment());

            foreach (var pic in data.Pics)
            {
                var picUrl = GetLargePicUrl(pic);
                if (picUrl != null)
                    segments.Add(picUrl.ToMilkyImageSegment());
            }
            return;
        }

        // no pics
        var content = text.ToString().Trim();
        if (string.IsNullOrEmpty(content))
            content = "[微博] 暂无可用文本内容";

        segments.Add(content.ToMilkyTextSegment());
    }

    private static string? GetLargePicUrl(WeiboPic pic)
    {
        return pic.Large?.Url ?? pic.Url;
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        var result = html;

        // block tags -> newline
        result = Regex.Replace(result, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"</p>", "\n", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<p[^>]*>", "", RegexOptions.IgnoreCase);

        // <a ...>inner</a> -> inner
        result = Regex.Replace(result, @"<a[^>]*>(.*?)</a>", "$1", RegexOptions.IgnoreCase);

        // remove all other tags
        result = Regex.Replace(result, @"<[^>]+>", "");

        // html decode
        result = WebUtility.HtmlDecode(result);

        return result.Trim();
    }
}