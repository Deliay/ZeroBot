using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

/// <summary>
/// 语音合成：/学:{alias}:{text}
/// 通过别名解析 dataset-id，调用合成接口并把音频发送到群聊。
/// 仅群聊可用，任何用户均可使用；同群同人每天最多生成 3 条（UTC+8 0 点刷新）。
/// </summary>
public class SynthesizeCommandHandler(
    ICommandDispatcher dispatcher,
    IBotContext bot,
    IJsonConfig<SynthesizeOptions> config,
    SynthesizeApi api) : CommandHandler(dispatcher)
{
    private static readonly char[] ArgumentSeparators = [':', '：', '-'];

    private static readonly OutgoingSegment HelpStrings =
        "/学:{alias}:{text}\n示例：/学:小松绿:你好呀".ToMilkyTextSegment();

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.Scene == MessageScene.Group
                                     && message.ToText().Trim().StartsWith("/学"));
    }

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var raw = message.ToText().Trim();
        if (!TryParse(raw, out var alias, out var text))
        {
            await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
            return;
        }

        var options = config.Current;
        if (!options.DatasetAliases.TryGetValue(alias, out var datasetId) || string.IsNullOrWhiteSpace(datasetId))
        {
            // 别名不存在：不处理。
            return;
        }

        if (!await TryConsumeQuotaAsync(message.Data.PeerId, message.Data.SenderId, cancellationToken))
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                [$"今日生成次数已用完（上限 {options.DailyLimit} 条/天），请明天再试。".ToMilkyTextSegment()]);
            return;
        }

        var bytes = await api.SynthesizeAsync(options.Endpoint, datasetId, text, options.Lang, cancellationToken);
        if (bytes is null)
        {
            await ReleaseQuotaAsync(message.Data.PeerId, message.Data.SenderId, cancellationToken);
            await message.ReplyAsGroup(bot, cancellationToken,
                ["语音合成失败，请稍后重试。".ToMilkyTextSegment()]);
            return;
        }

        var record = new RecordOutgoingSegment(
            new RecordOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}")));
        await message.SendAsGroup(bot, cancellationToken, [record]);
    }

    /// <summary>
    /// 解析 /学:{alias}:{text}，text 保留原始内容（可含分隔符）。
    /// </summary>
    private static bool TryParse(string raw, out string alias, out string text)
    {
        alias = string.Empty;
        text = string.Empty;
        const string command = "/学";
        if (!raw.StartsWith(command)) return false;

        var rest = raw[command.Length..];
        if (rest.Length == 0 || !ArgumentSeparators.Contains(rest[0])) return false;

        var body = rest[1..];
        var index = body.IndexOfAny(ArgumentSeparators);
        if (index < 0) return false;

        alias = body[..index].Trim();
        text = body[(index + 1)..];
        return alias.Length > 0 && text.Length > 0;
    }

    private async ValueTask<bool> TryConsumeQuotaAsync(long peerId, long senderId,
        CancellationToken cancellationToken)
    {
        var key = SynthesizeQuota.Key(peerId, senderId);
        var today = SynthesizeQuota.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        return await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            if (!value.DailyQuotas.TryGetValue(key, out var quota) || quota.Date != today)
            {
                value.DailyQuotas[key] = quota = new DailyQuota(today, 0);
            }

            if (quota.Count >= value.DailyLimit) return false;

            value.DailyQuotas[key] = quota with { Count = quota.Count + 1 };
            await config.SaveAsync(value, token);
            return true;
        }, cancellationToken);
    }

    private async ValueTask ReleaseQuotaAsync(long peerId, long senderId,
        CancellationToken cancellationToken)
    {
        var key = SynthesizeQuota.Key(peerId, senderId);
        var today = SynthesizeQuota.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            if (value.DailyQuotas.TryGetValue(key, out var quota)
                && quota.Date == today
                && quota.Count > 0)
            {
                value.DailyQuotas[key] = quota with { Count = quota.Count - 1 };
                await config.SaveAsync(value, token);
            }
        }, cancellationToken);
    }
}
