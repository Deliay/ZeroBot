using System.Text;
using Milky.Net.Model;
using ZeroBot.Utility;

namespace ZeroBot.Bilibili.Dynamic;

public static class DynamicMessageBuilder
{
    private const string ForwardType = "DYNAMIC_TYPE_FORWARD";

    public static OutgoingSegment[] Build(DynamicData data)
    {
        var segments = new List<OutgoingSegment>();
        AppendDynamic(data, segments);
        return [.. segments];
    }

    private static void AppendDynamic(DynamicData data, List<OutgoingSegment> segments)
    {
        var moduleDynamic = data.Modules?.ModuleDynamic;
        var opus = moduleDynamic?.Major?.Opus;
        var text = new StringBuilder();

        // forward dynamics put their own words in desc
        if (data.Type == ForwardType && moduleDynamic?.Desc != null)
            text.Append(RenderRichText(moduleDynamic.Desc));

        if (opus != null)
        {
            if (!string.IsNullOrWhiteSpace(opus.Title)) text.AppendLine(opus.Title.Trim());
            text.Append(RenderRichText(opus.Summary));
            var link = NormalizeUrl(opus.JumpUrl);
            if (link != null) text.Append('\n').Append(link);
        }

        // fallback to desc for non-forward dynamics without opus
        if (text.Length == 0 && moduleDynamic?.Desc != null)
            text.Append(RenderRichText(moduleDynamic.Desc));
        if (text.Length == 0)
            text.Append($"[{data.Type}] 暂无可用文本内容");

        // pics are shown after the text, separated by a newline
        if (opus is { Pics.Count: > 0 }) text.Append('\n');
        segments.Add(text.ToString().ToMilkyTextSegment());

        if (opus != null)
        {
            foreach (var pic in opus.Pics)
            {
                var url = NormalizeUrl(pic.Url);
                if (url != null) segments.Add(url.ToMilkyImageSegment());
            }
        }

        if (data.Type == ForwardType && data.Orig != null)
        {
            segments.Add("\n---- 转发 ----\n".ToMilkyTextSegment());
            AppendDynamic(data.Orig, segments);
        }
    }

    private static string RenderRichText(DynamicRichText? richText)
    {
        if (richText == null) return "";
        if (richText.RichTextNodes.Count == 0) return richText.Text;
        var builder = new StringBuilder();
        foreach (var node in richText.RichTextNodes)
            builder.Append(node.Text);
        return builder.ToString();
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("//")) return $"https:{url}";
        if (url.StartsWith("http://")) return $"https://{url["http://".Length..]}";
        return url;
    }
}
