using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Painter;

/// <summary>
/// 公共素材：/小画家:公共素材（列出）、/小画家:公共素材:添加:{name}（同名覆盖）、/小画家:公共素材:删除:{name}。
/// 仅群聊，且本群必须已通过 /小画家:启用 开启（未开启的群完全静默，与 /小画家:画 一致）。
/// 增 / 删需高权限（sudoers / 群管理员 / 拥有 painter.manage 权限者），列出对所有群员开放。
/// 「添加」取本条消息上下文中的图片：优先引用消息中的图片，其次本消息中的图片，最多 MaxImages 张。
/// </summary>
public class PainterMaterialCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    PainterMaterialStore store,
    IJsonConfig<PainterOptions> config,
    ILogger<PainterMaterialCommandHandler> logger) : CommandHandler(dispatcher)
{
    private const string CommandPrefix = "/小画家:公共素材";
    private const string CommandPrefixFullWidth = "/小画家：公共素材";

    /// <summary>列表一次性最多展示的素材数，超出只提示总数。</summary>
    private const int MaxListed = 50;

    private static readonly OutgoingSegment HelpStrings =
        ("/小画家:公共素材 - 列出本群公共素材\n" +
         "/小画家:公共素材:添加:{name} - 把本条消息（或引用消息）中的图片添加为公共素材，同名覆盖\n" +
         "/小画家:公共素材:删除:{name} - 删除本群公共素材").ToMilkyTextSegment();

    /// <summary>
    /// 解析 /小画家:公共素材[:{动作}[:{name}]]。
    /// <paramref name="action"/> 为空表示列出；name 为动作之后首个分隔符之后的全部原文
    /// （保留空格 / 连字符等，仅修剪首尾空白）。分隔符只认 <c>:</c> 与 <c>：</c>。
    /// </summary>
    public static bool TryParse(string raw, out string action, out string name)
    {
        action = string.Empty;
        name = string.Empty;

        string rest;
        if (raw.StartsWith(CommandPrefix))
        {
            rest = raw[CommandPrefix.Length..];
        }
        else if (raw.StartsWith(CommandPrefixFullWidth))
        {
            rest = raw[CommandPrefixFullWidth.Length..];
        }
        else
        {
            return false;
        }

        if (rest.Length == 0) return true;
        if (rest[0] is not (':' or '：')) return false;

        rest = rest[1..];
        var separator = rest.IndexOfAny([':', '：']);
        if (separator < 0)
        {
            action = rest.Trim();
            return true;
        }

        action = rest[..separator].Trim();
        name = rest[(separator + 1)..].Trim();
        return true;
    }

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return false;
        if (!TryParse(message.ToText().Trim(), out var action, out _)) return false;

        // 未开启绘图的群完全静默（不回复、不贴表情）；与 /小画家:画 的谓词保持一致。
        if (!config.Current.Groups.ContainsKey(message.Data.PeerId)) return false;

        // 列出对所有群员开放；增删需高权限（无权限 → 谓词 false → 静默）。
        if (action is not ("添加" or "删除")) return true;

        return await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message,
            PainterManageCommandHandler.PermissionName, cancellationToken);
    }

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(message.ToText().Trim(), out var action, out var name))
        {
            await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
            return;
        }

        switch (action)
        {
            case "":
                await ListAsync(message, cancellationToken);
                return;
            case "添加":
                await AddAsync(message, name, cancellationToken);
                return;
            case "删除":
                await DeleteAsync(message, name, cancellationToken);
                return;
            default:
                await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
        }
    }

    private async ValueTask ListAsync(Event<IncomingMessage> message, CancellationToken cancellationToken)
    {
        var materials = store.List(message.Data.PeerId);
        if (materials.Count == 0)
        {
            await message.ReplyAsGroup(bot, cancellationToken, ["本群暂无公共素材。".ToMilkyTextSegment()]);
            return;
        }

        var lines = new List<string> { $"本群公共素材（{materials.Count} 个）：" };
        lines.AddRange(materials.Take(MaxListed).Select(m => $"· {m.Name}（{m.Files.Count} 张）"));
        if (materials.Count > MaxListed)
        {
            lines.Add($"……其余 {materials.Count - MaxListed} 个未显示");
        }

        await message.ReplyAsGroup(bot, cancellationToken, [string.Join('\n', lines).ToMilkyTextSegment()]);
    }

    private async ValueTask AddAsync(Event<IncomingMessage> message, string name,
        CancellationToken cancellationToken)
    {
        if (!PainterMaterialStore.IsValidName(name, out var nameError))
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                [$"添加失败：{nameError}".ToMilkyTextSegment()]);
            return;
        }

        var maxImages = config.Current.MaxImages;
        List<byte[]> images;
        try
        {
            images = await PainterImages.CollectContextImagesAsync(bot, message, maxImages, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "添加公共素材时读取图片失败：peer={PeerId}", message.Data.PeerId);
            await message.ReplyAsGroup(bot, cancellationToken,
                ["添加失败：读取图片出错，请稍后重试。".ToMilkyTextSegment()]);
            return;
        }

        if (images.Count == 0)
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                ["没有找到可添加的图片：请随消息发送图片，或引用一条含图片的消息。".ToMilkyTextSegment()]);
            return;
        }

        var payload = images
            .Select(bytes => (Bytes: bytes, Extension: PainterApi.DetectImageType(bytes).Extension))
            .ToList();

        await store.AddAsync(message.Data.PeerId, name, payload, cancellationToken);
        await message.ReplyAsGroup(bot, cancellationToken,
            [$"已添加公共素材「{name}」（{images.Count} 张图片）。".ToMilkyTextSegment()]);
    }

    private async ValueTask DeleteAsync(Event<IncomingMessage> message, string name,
        CancellationToken cancellationToken)
    {
        if (!PainterMaterialStore.IsValidName(name, out var nameError))
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                [$"删除失败：{nameError}".ToMilkyTextSegment()]);
            return;
        }

        var deleted = await store.DeleteAsync(message.Data.PeerId, name, cancellationToken);
        await message.ReplyAsGroup(bot, cancellationToken,
            [(deleted ? $"已删除公共素材「{name}」。" : $"未找到公共素材「{name}」。").ToMilkyTextSegment()]);
    }
}
