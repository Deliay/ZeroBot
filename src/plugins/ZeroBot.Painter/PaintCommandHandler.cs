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
/// </summary>
public class PaintCommandHandler(
    ICommandDispatcher dispatcher,
    IBotContext bot,
    IJsonConfig<PainterOptions> config,
    PainterApi api,
    ILogger<PaintCommandHandler> logger) : CommandQueuedHandler(dispatcher)
{
    private const string CommandPrefix = "/小画家:画";
    private const string CommandPrefixFullWidth = "/小画家：画";

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

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return ValueTask.FromResult(false);
        if (!TryParsePrompt(message.ToText().Trim(), out _)) return ValueTask.FromResult(false);

        // 未开启的群 → false → CommandDispatcher 不分发，完全静默（不回复、不贴表情、不计数、不调用接口）。
        return ValueTask.FromResult(config.Current.Groups.ContainsKey(message.Data.PeerId));
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = @event.ToText().Trim();
            if (!TryParsePrompt(raw, out var prompt))
            {
                // 理论上谓词已挡，防御性分支。
                await @event.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["提示词不能为空，请使用：/小画家:画:{prompt}".ToMilkyTextSegment()]);
                return;
            }

            var peerId = @event.Data.PeerId;
            var senderId = @event.Data.SenderId;
            var options = config.Current;
            if (!options.Groups.TryGetValue(peerId, out var limit))
            {
                // 谓词已保证开启，取不到兜底静默。
                return;
            }

            if (!await TryConsumeQuotaAsync(peerId, senderId, limit, cancellationToken))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    [$"你今天在本群的绘图次数已用完（{limit} 张），明天再来吧。".ToMilkyTextSegment()]);
                return;
            }

            byte[]? bytes;
            try
            {
                var images = new List<byte[]>();
                foreach (var seg in @event.Data.Segments.OfType<ImageIncomingSegment>().Take(options.MaxImages))
                {
                    images.Add(await seg.GetMilkyImageBytesAsync(bot, @event, cancellationToken));
                }

                bytes = await api.GenerateAsync(options.Endpoint, prompt, images,
                    TimeSpan.FromSeconds(options.HttpTimeoutSeconds), cancellationToken);
            }
            catch (Exception e)
            {
                // 取图或调用异常：仅记日志并按失败处理；用量已消耗，不回滚。
                logger.LogError(e, "绘图调用异常");
                bytes = null;
            }

            if (bytes is null)
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["绘图失败，请稍后重试。".ToMilkyTextSegment()]);
                return;
            }

            await @event.ReplyAsGroup(bot, cancellationToken, [bytes.ToMilkyImageSegment()]);
        }
        catch (Exception e)
        {
            logger.LogError(e, "处理绘图指令时发生异常");
        }
        finally
        {
            await @event.RemoveReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
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

            if (quota.Count >= limit) return false;

            value.DailyQuotas[key] = quota with { Count = quota.Count + 1 };
            await config.SaveAsync(value, token);
            return true;
        }, cancellationToken);
    }
}
