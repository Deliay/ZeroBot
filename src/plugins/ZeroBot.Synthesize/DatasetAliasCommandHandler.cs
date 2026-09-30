using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

/// <summary>
/// 数据集别名管理：/synthesize:dataset:{dataset-id}:{alias}
/// 兼容 /synthesize:{dataset-id}:{alias}。
/// 仅高权限用户（sudoers / 群管理员 / 拥有 synthesize.alias 权限者）可用。
/// </summary>
public class DatasetAliasCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<SynthesizeOptions> config) : CommandHandler(dispatcher)
{
    public const string PermissionName = "synthesize.alias";

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
         "示例：/synthesize:dataset:6aba45e18826119a12d738a4:小松绿").ToMilkyTextSegment();

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
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
