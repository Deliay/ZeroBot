using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

/// <summary>
/// 数据集别名与额度管理：/synthesize:dataset:{dataset-id}:{alias}
/// 兼容 /synthesize:{dataset-id}:{alias}；另支持 /synthesize:limit:{number} 设置本群每人每日上限。
/// 仅高权限用户（sudoers / 群管理员 / 拥有 synthesize.alias 权限者）可用。
/// </summary>
public class DatasetAliasCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<SynthesizeOptions> config) : CommandHandler(dispatcher)
{
    public const string PermissionName = "synthesize.alias";

    private const string LimitPrefix = "/synthesize:limit:";
    private const string LimitPrefixFullWidth = "/synthesize：limit：";

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return text.StartsWith("/synthesize")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, PermissionName,
                   cancellationToken);
    }

    private static readonly OutgoingSegment HelpStrings =
        ("/synthesize:dataset:{dataset-id}:{alias}\n" +
         "/synthesize:limit:{number}（-1 不限制，0 禁止，>0 为每人每日上限）\n" +
         "示例：/synthesize:dataset:6aba45e18826119a12d738a4:小松绿").ToMilkyTextSegment();

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var raw = message.ToText().Trim();
        if (TryGetLimitArgument(raw, out var limitValue))
        {
            await HandleLimitAsync(message, limitValue, cancellationToken);
            return;
        }

        var command = message.Data.ToTextCommands().FirstOrDefault();
        if (command is null)
        {
            await message.Reply(bot, cancellationToken, [HelpStrings]);
            return;
        }

        var (datasetId, alias) = Parse(command.Arguments);
        if (string.IsNullOrWhiteSpace(datasetId) || string.IsNullOrWhiteSpace(alias))
        {
            await message.Reply(bot, cancellationToken, [HelpStrings]);
            return;
        }

        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            value.DatasetAliases[alias] = datasetId;
            await config.SaveAsync(value, token);
            await message.Reply(bot, token,
                [$"数据集别名绑定成功：{alias} -> {datasetId}".ToMilkyTextSegment()]);
        }, cancellationToken);
    }

    /// <summary>
    /// 解析 /synthesize:limit:{number} 的数字部分（保留负号，避免被命令分隔符拆开）。
    /// </summary>
    private static bool TryGetLimitArgument(string raw, out string value)
    {
        value = string.Empty;
        if (raw.StartsWith(LimitPrefix))
        {
            value = raw[LimitPrefix.Length..];
            return true;
        }

        if (raw.StartsWith(LimitPrefixFullWidth))
        {
            value = raw[LimitPrefixFullWidth.Length..];
            return true;
        }

        return false;
    }

    private async ValueTask HandleLimitAsync(Event<IncomingMessage> message, string rawValue,
        CancellationToken cancellationToken)
    {
        if (message.Scene != MessageScene.Group)
        {
            await message.Reply(bot, cancellationToken,
                ["该指令仅可在群聊中使用。".ToMilkyTextSegment()]);
            return;
        }

        if (!int.TryParse(rawValue.Trim(), out var limit))
        {
            await message.Reply(bot, cancellationToken,
                ["参数无效，请输入整数：/synthesize:limit:{number}（-1 不限制，0 禁止，>0 为上限）"
                    .ToMilkyTextSegment()]);
            return;
        }

        var peerId = message.Data.PeerId;
        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            value.GroupDailyLimits[peerId] = limit;
            await config.SaveAsync(value, token);

            var description = limit switch
            {
                < 0 => "不限制",
                0 => "禁止生成",
                _ => $"每人每天最多 {limit} 条"
            };
            await message.Reply(bot, token,
                [$"本群语音合成额度已设置：{description}。".ToMilkyTextSegment()]);
        }, cancellationToken);
    }

    private static (string? DatasetId, string? Alias) Parse(string[] arguments)
    {
        return arguments switch
        {
            // /synthesize:dataset:{dataset-id}:{alias}
            ["dataset", var datasetId, var alias, ..] => (datasetId, alias),
            // /synthesize:{dataset-id}:{alias}
            [var datasetId, var alias, ..] => (datasetId, alias),
            _ => (null, null)
        };
    }
}
