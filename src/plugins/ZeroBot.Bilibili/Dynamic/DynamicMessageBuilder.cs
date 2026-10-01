using System.Text;
using System.Text.Json;
using Milky.Net.Model;
using ZeroBot.Utility;

namespace ZeroBot.Bilibili.Dynamic;

public static class DynamicMessageBuilder
{
    private const string ForwardType = "DYNAMIC_TYPE_FORWARD";
    private const string LiveRcmdType = "DYNAMIC_TYPE_LIVE_RCMD";

    public static OutgoingSegment[] Build(DynamicData data, string? fallbackAuthor = null)
    {
        var author = data.Modules?.ModuleAuthor?.Name;
        if (string.IsNullOrWhiteSpace(author)) author = fallbackAuthor ?? "神秘人";
        var segments = new List<OutgoingSegment>
        {
            $"{author} 发布了新动态".ToMilkyTextSegment(),
        };
        AppendDynamic(data, segments);
        return [.. segments];
    }

    private static string? GetDynamicUrl(DynamicData data)
    {
        var opus = data.Modules?.ModuleDynamic?.Major?.Opus;
        var link = NormalizeUrl(opus?.JumpUrl);
        if (link != null) return link;
        return data.IdStr is { Length: > 0 } ? $"https://www.bilibili.com/opus/{data.IdStr}" : null;
    }

    private static void AppendDynamic(DynamicData data, List<OutgoingSegment> segments, bool isOrig = false)
    {
        if (data.Type == LiveRcmdType)
        {
            AppendLiveRcmd(data, segments);
            return;
        }

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
            if (isOrig)
            {
                var origLink = GetDynamicUrl(data);
                if (origLink != null) text.Append('\n').Append($"原动态：{origLink}");
            }
            else if (data.Type != ForwardType || data.Orig == null)
            {
                var link = NormalizeUrl(opus.JumpUrl);
                if (link != null) text.Append('\n').Append(link);
            }
        }

        // fallback to desc for non-forward dynamics without opus
        if (text.Length == 0 && moduleDynamic?.Desc != null)
            text.Append(RenderRichText(moduleDynamic.Desc));
        if (text.Length == 0)
            text.Append($"[{data.Type}] 暂无可用文本内容");

        if (data.Type == ForwardType && data.Orig != null)
        {
            var forwardUrl = GetDynamicUrl(data);
            if (forwardUrl != null) text.Append('\n').Append(forwardUrl);

            if (opus is { Pics.Count: > 0 }) text.Append('\n');
            segments.Add(text.ToString().ToMilkyTextSegment());

            if (opus != null)
            {
                foreach (var pic in opus.Pics)
                {
                    var picUrl = NormalizeUrl(pic.Url);
                    if (picUrl != null) segments.Add(picUrl.ToMilkyImageSegment());
                }
            }

            segments.Add("\n---- 转发 ----\n".ToMilkyTextSegment());
            AppendDynamic(data.Orig, segments, isOrig: true);
            return;
        }

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
    }

    private static void AppendLiveRcmd(DynamicData data, List<OutgoingSegment> segments)
    {
        var text = new StringBuilder("[正在直播]");
        string? cover = null;
        var content = data.Modules?.ModuleDynamic?.Major?.LiveRcmd?.Content;
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var liveContent = JsonSerializer.Deserialize<LiveRcmdContent>(content);
                var info = liveContent?.LivePlayInfo;
                if (info != null)
                {
                    if (!string.IsNullOrWhiteSpace(info.Title)) text.Append(' ').Append(info.Title.Trim());
                    cover = NormalizeUrl(info.Cover);
                    var link = NormalizeUrl(info.Link);
                    if (link != null) text.Append('\n').Append(link);
                }
            }
            catch (JsonException)
            {
                // ignore malformed live_rcmd content, fall through to placeholder text
            }
        }
        segments.Add(text.ToString().ToMilkyTextSegment());
        if (cover != null) segments.Add(cover.ToMilkyImageSegment());
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

    /// <summary>提取动态纯文本（供语音播报）：标题 + 富文本正文，转发链递归拼接，剔除图片、表情占位与链接。</summary>
    public static string BuildVoiceText(DynamicData data)
    {
        var builder = new StringBuilder();
        AppendVoiceText(data, builder);
        return builder.ToString().Trim();
    }

    private static void AppendVoiceText(DynamicData data, StringBuilder target)
    {
        if (data.Type == LiveRcmdType)
        {
            var liveText = BuildLiveRcmdVoiceText(data);
            if (!string.IsNullOrWhiteSpace(liveText)) target.Append(liveText);
            return;
        }

        var moduleDynamic = data.Modules?.ModuleDynamic;
        var opus = moduleDynamic?.Major?.Opus;

        // 每层用局部 builder 收集本层文本，避免父层已有内容影响「本层无文本则回退 desc」的判断
        var local = new StringBuilder();

        // 转发动态用自己的话写在 desc
        if (data.Type == ForwardType && moduleDynamic?.Desc != null)
            local.Append(RenderRichText(moduleDynamic.Desc));

        if (opus != null)
        {
            if (!string.IsNullOrWhiteSpace(opus.Title)) local.Append(opus.Title.Trim()).Append('\n');
            local.Append(RenderRichText(opus.Summary));
        }

        // 回退 desc（对齐 AppendDynamic 的 fallback）；不追加图片、链接与占位展示文案
        if (local.Length == 0 && moduleDynamic?.Desc != null)
            local.Append(RenderRichText(moduleDynamic.Desc));

        if (local.Length > 0) target.Append(local);

        if (data.Type == ForwardType && data.Orig != null)
        {
            if (target.Length > 0) target.Append('\n');
            AppendVoiceText(data.Orig, target);
        }
    }

    private static string? BuildLiveRcmdVoiceText(DynamicData data)
    {
        var content = data.Modules?.ModuleDynamic?.Major?.LiveRcmd?.Content;
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            var info = JsonSerializer.Deserialize<LiveRcmdContent>(content)?.LivePlayInfo;
            if (info == null || string.IsNullOrWhiteSpace(info.Title)) return null;
            return $"[正在直播] {info.Title.Trim()}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("//")) return $"https:{url}";
        if (url.StartsWith("http://")) return $"https://{url["http://".Length..]}";
        return url;
    }
}
