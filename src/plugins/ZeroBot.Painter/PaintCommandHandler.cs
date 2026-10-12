using System.Text;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Painter;

/// <summary>
/// 绘图指令：/小画家:画:{prompt}
/// 仅群聊可用，任何群员均可使用；本群必须已通过 /小画家:启用 开启，未开启的群完全静默。
/// 同群同人每天（UTC+8）受该群上限限制，通过限额检查后、调用接口之前即消耗一次用量，
/// 失败调用同样计入且不回滚。
///
/// 参赛图片的优先级：引用消息中的图片 &gt; 本条消息中的图片 &gt; 提示词开头 <c>{name}</c> 引用的公共素材图片。
/// 提示词开头的 <c>{name}</c> 引用命中本群公共素材时从提示词中移除并追加其图片，
/// 未命中则原样保留（不替换）。
///
/// 群上限在请求发起时（派发阶段）捕获，排队期间管理员禁用/调整上限不影响在途请求，
/// 保证「以发起时状态为准，进行中的调用完成并回发结果」（PRD §5）。
/// </summary>
public class PaintCommandHandler(
    ICommandDispatcher dispatcher,
    IBotContext bot,
    IJsonConfig<PainterOptions> config,
    PainterApi api,
    PainterMaterialStore store,
    ILogger<PaintCommandHandler> logger) : CommandQueuedHandler(dispatcher)
{
    private const string CommandPrefix = "/小画家:画";
    private const string CommandPrefixFullWidth = "/小画家：画";

    /// <summary>
    /// 入队前捕获的「发起时群上限」，key = (PeerId, MessageSeq)；DequeueAsync 入口取出即移除。
    /// </summary>
    private readonly PaintRequestLimits _requestLimits = new();

    private static readonly OutgoingSegment HelpStrings =
        "/小画家:画:{prompt}\n示例：/小画家:画:一只在樱花树下打盹的猫".ToMilkyTextSegment();

    /// <summary>
    /// 解析 /小画家:画:{prompt}；prompt 为首个分隔符（: 或 ：）之后的全部原文，保留冒号/连字符。
    /// 命中指令但无 prompt（如 "/小画家:画"、"/小画家:画:"）返回 true 且 prompt 为空；
    /// "/小画家:画册..." 之类不是本指令，返回 false。
    /// </summary>
    public static bool TryParsePrompt(string raw, out string prompt)
    {
        prompt = string.Empty;

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

        if (rest.Length == 0)
        {
            return true;
        }

        if (rest[0] is not (':' or '：'))
        {
            return false;
        }

        prompt = rest[1..];
        return true;
    }

    /// <summary>
    /// 切出提示词开头连续的 <c>{name}</c> 引用。
    /// 返回每个引用的原文（含花括号）与其中的名称，以及引用之后的剩余提示词；
    /// 首字符不是 <c>{</c>、<c>{}</c> 或缺少右花括号时停止切分（后续内容整体作为剩余提示词）。
    /// 名称非法（空）的引用视为普通文本，停止切分。
    /// </summary>
    public static (List<(string Raw, string Name)> References, string Remaining) SplitMaterialReferences(string prompt)
    {
        var references = new List<(string Raw, string Name)>();
        var remaining = prompt;

        while (remaining.Length > 1 && remaining[0] == '{')
        {
            var close = remaining.IndexOf('}');
            if (close <= 1) break;

            var name = remaining[1..close].Trim();
            if (name.Length == 0) break;

            references.Add((remaining[..(close + 1)], name));
            remaining = remaining[(close + 1)..];
        }

        return (references, remaining);
    }

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return ValueTask.FromResult(false);
        if (!TryParsePrompt(message.ToText().Trim(), out _)) return ValueTask.FromResult(false);

        // 未开启的群 → false → CommandDispatcher 不分发，完全静默（不回复、不贴表情、不计数、不调用接口）。
        return ValueTask.FromResult(config.Current.Groups.ContainsKey(message.Data.PeerId));
    }

    /// <summary>
    /// 在入队（贴表情 + 写入串行队列）之前，记录本请求发起时的群上限。
    /// </summary>
    protected override async ValueTask HandleAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        if (!config.Current.Groups.TryGetValue(@event.Data.PeerId, out var limit))
        {
            // 谓词刚通过、此处状态已变化（如热加载禁用）：静默，不贴表情、不入队。
            return;
        }

        _requestLimits.Record(@event.Data.PeerId, @event.Data.MessageSeq, limit);
        try
        {
            await base.HandleAsync(@event, cancellationToken);
        }
        catch
        {
            // 入队失败时清理，避免状态泄漏。
            _requestLimits.TryTake(@event.Data.PeerId, @event.Data.MessageSeq, out _);
            throw;
        }
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var peerId = @event.Data.PeerId;
            var senderId = @event.Data.SenderId;

            // 消费第一步即取出并移除「发起时上限」，保证后续所有分支
            //（解析失败 / 空 prompt / 额度用尽 / 正常处理）都不会残留记录。
            // 使用发起时捕获的上限：队列等待期间管理员禁用/改上限不影响本请求（PRD §5）。
            if (!_requestLimits.TryTake(peerId, @event.Data.MessageSeq, out var limit))
            {
                logger.LogWarning("未找到绘图请求的发起时群上限，跳过处理：peer={PeerId} seq={Seq}",
                    peerId, @event.Data.MessageSeq);
                return;
            }

            var raw = @event.ToText().Trim();
            if (!TryParsePrompt(raw, out var prompt))
            {
                // 理论上谓词已挡，防御性分支。
                await @event.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
            }

            // 提示词开头的 {name} 引用本群公共素材：命中则移除占位符并追加其图片，未命中则保留原文。
            var (references, remainingPrompt) = SplitMaterialReferences(prompt.TrimStart());
            var materialNames = new List<string>();
            var builder = new StringBuilder();
            foreach (var (rawReference, materialName) in references)
            {
                if (store.Find(peerId, materialName) is null)
                {
                    builder.Append(rawReference);
                    continue;
                }

                if (!materialNames.Contains(materialName)) materialNames.Add(materialName);
            }

            builder.Append(remainingPrompt);
            var finalPrompt = builder.ToString();

            if (string.IsNullOrWhiteSpace(finalPrompt))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["提示词不能为空，请使用：/小画家:画:{prompt}".ToMilkyTextSegment()]);
                return;
            }

            if (!await TryConsumeQuotaAsync(peerId, senderId, limit, cancellationToken))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    [$"你今天在本群的绘图次数已用完（{limit} 张），明天再来吧。".ToMilkyTextSegment()]);
                return;
            }

            PaintResult result;
            try
            {
                var options = config.Current;

                // 优先级：引用消息的图片 > 本条消息的图片 > 提示词引用素材的图片，总计不超过 MaxImages 张。
                var images = await PainterImages.CollectContextImagesAsync(bot, @event, options.MaxImages,
                    cancellationToken);
                foreach (var materialName in materialNames)
                {
                    if (images.Count >= options.MaxImages) break;

                    foreach (var materialImage in await store.LoadImagesAsync(peerId, materialName, cancellationToken))
                    {
                        if (images.Count >= options.MaxImages) break;
                        images.Add(materialImage);
                    }
                }

                result = await api.GenerateAsync(options.Endpoint, finalPrompt, images,
                    TimeSpan.FromSeconds(options.HttpTimeoutSeconds), cancellationToken);
            }
            catch (Exception e)
            {
                // 取图或调用异常：仅记日志并按失败处理；用量已消耗，不回滚。
                logger.LogError(e, "绘图调用异常");
                result = PaintResult.Fail(null);
            }

            if (!result.Success || result.Data is not { Length: > 0 } image)
            {
                // 带上上游返回的错误详情（PainterApi 已截断），便于用户与运维定位问题。
                var message = string.IsNullOrWhiteSpace(result.Error)
                    ? "绘图失败，请稍后重试。"
                    : $"绘图失败：{result.Error}";
                await @event.ReplyAsGroup(bot, cancellationToken, [message.ToMilkyTextSegment()]);
                return;
            }

            await @event.ReplyAsGroup(bot, cancellationToken, [image.ToMilkyImageSegment()]);
        }
        catch (Exception e)
        {
            logger.LogError(e, "处理绘图指令时发生异常");
        }
        finally
        {
            // 摘除表情必须自行兜底：finally 内的异常会直接逃出 DequeueAsync，
            // 而 CommandQueuedHandler 串行消费循环没有 try/catch，一旦抛出会永久中断绘图队列。
            try
            {
                await @event.RemoveReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "移除处理中表情失败");
            }
        }
    }

    protected override ValueTask EnqueueInspectorAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
        => @event.AddReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);

    /// <summary>
    /// 在配置 mutation scope 内原子「检查并自增」，失败不回滚。
    /// </summary>
    private async ValueTask<bool> TryConsumeQuotaAsync(long peerId, long senderId, int limit,
        CancellationToken cancellationToken)
    {
        var key = PainterQuota.Key(peerId, senderId);
        var today = PainterQuota.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        return await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            if (!value.DailyQuotas.TryGetValue(key, out var quota) || quota.Date != today)
            {
                value.DailyQuotas[key] = quota = new DailyQuota(today, 0);
            }

            if (limit > -1 && quota.Count >= limit) return false;

            value.DailyQuotas[key] = quota with { Count = quota.Count + 1 };
            await config.SaveAsync(value, token);
            return true;
        }, cancellationToken);
    }
}
