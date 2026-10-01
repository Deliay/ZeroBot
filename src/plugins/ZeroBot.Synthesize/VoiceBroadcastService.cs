using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Synthesize.Abstraction;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

/// <summary>
/// 通知语音播报实现。按群解析音色别名，同一别名（音色）的多个群只合成一次并分发，
/// 失败完全隔离：任何异常仅记日志，不影响调用方的通知主流程。
/// </summary>
public class VoiceBroadcastService(
    IJsonConfig<SynthesizeOptions> config,
    SynthesizeApi api,
    IBotContext bot,
    ILogger<VoiceBroadcastService> logger) : IVoiceBroadcaster
{
    public async ValueTask BroadcastAsync(IReadOnlyCollection<long> groupIds, string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await config.WaitForInitializedAsync(cancellationToken);
            var options = config.Current;

            // 只保留已开启播报的群
            var targets = groupIds.Where(g => options.VoiceBroadcastGroups.ContainsKey(g)).ToHashSet();
            if (targets.Count == 0) return;

            var prepared = PrepareText(text, options.VoiceBroadcastMaxTextLength);
            if (prepared is null) return;

            // 按音色别名分组，同一 datasetId 只合成一次后分发给组内全部群
            foreach (var group in targets.GroupBy(g => options.VoiceBroadcastGroups[g]))
            {
                var alias = group.Key;
                if (!options.DatasetAliases.TryGetValue(alias, out var datasetId)
                    || string.IsNullOrWhiteSpace(datasetId))
                {
                    logger.LogWarning("Voice broadcast alias {Alias} is not bound, skip", alias);
                    continue;
                }

                var bytes = await api.SynthesizeAsync(options.Endpoint, datasetId, prepared, options.Lang,
                    cancellationToken);
                if (bytes is null)
                {
                    logger.LogWarning(
                        "Voice broadcast synthesize failed, alias: {Alias}, dataset: {DatasetId}", alias, datasetId);
                    continue;
                }

                var record = new RecordOutgoingSegment(
                    new RecordOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}")));
                var groups = group.ToHashSet();
                await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
                {
                    await bot.WriteManyGroupMessageAsync(accountId, groups, cancellationToken, [record]);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "VoiceBroadcastService BroadcastAsync exception");
        }
    }

    /// <summary>
    /// 播报文本预处理：Trim 后为空返回 null（跳过播报）；超过 maxLength 截断（maxLength &lt;= 0 不截断）。
    /// </summary>
    public static string? PrepareText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (maxLength > 0 && trimmed.Length > maxLength) trimmed = trimmed[..maxLength];
        return trimmed;
    }
}
