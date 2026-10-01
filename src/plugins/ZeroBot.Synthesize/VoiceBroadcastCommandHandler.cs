using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

/// <summary>
/// 通知语音播报开关：/动态语音播报:启用:{alias} / /动态语音播报:禁用
/// 仅群聊可用（私聊静默不响应），高权限用户（sudoers / 群管理员 / 拥有 synthesize.voice-broadcast 权限者）可用。
/// </summary>
public class VoiceBroadcastCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<SynthesizeOptions> config) : CommandHandler(dispatcher)
{
    public const string PermissionName = "synthesize.voice-broadcast";

    private static readonly OutgoingSegment HelpStrings =
        ("/动态语音播报:启用:{alias}\n" +
         "/动态语音播报:禁用").ToMilkyTextSegment();

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        return message.Scene == MessageScene.Group
               && message.ToText().Trim().StartsWith("/动态语音播报")
               && await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, PermissionName,
                   cancellationToken);
    }

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var command = message.ToTextCommands().FirstOrDefault();
        var arguments = command?.Arguments ?? [];
        var peerId = message.Data.PeerId;

        switch (arguments)
        {
            case ["启用", var alias, ..] when !string.IsNullOrWhiteSpace(alias):
                await HandleEnableAsync(message, peerId, alias, cancellationToken);
                return;
            case ["启用", ..]:
                await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
            case ["禁用", ..]:
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.VoiceBroadcastGroups.Remove(peerId);
                    await config.SaveAsync(value, token);
                    await message.ReplyAsGroup(bot, token, ["已关闭本群通知语音播报".ToMilkyTextSegment()]);
                }, cancellationToken);
                return;
            default:
                await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
        }
    }

    private async ValueTask HandleEnableAsync(Event<IncomingMessage> message, long peerId, string alias,
        CancellationToken cancellationToken)
    {
        var options = config.Current;
        if (!options.DatasetAliases.TryGetValue(alias, out var datasetId) || string.IsNullOrWhiteSpace(datasetId))
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                [$"别名 {alias} 未绑定数据集，请先使用 /synthesize:dataset 绑定".ToMilkyTextSegment()]);
            return;
        }

        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            value.VoiceBroadcastGroups[peerId] = alias;
            await config.SaveAsync(value, token);
            await message.ReplyAsGroup(bot, token,
                [$"已开启本群通知语音播报，音色：{alias}".ToMilkyTextSegment()]);
        }, cancellationToken);
    }
}
