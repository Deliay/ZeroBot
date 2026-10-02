using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Painter;

/// <summary>
/// 群绘图功能管理：/小画家:启用:{number}（1~5）与 /小画家:禁用。
/// 仅群聊可用，高权限用户（sudoers / 群管理员 / 拥有 painter.manage 权限者）可用。
/// 启用时写入群上限配置，禁用时移除该群配置但不清除当日用量。
/// </summary>
public class PainterManageCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<PainterOptions> config) : CommandHandler(dispatcher)
{
    public const string PermissionName = "painter.manage";

    private static readonly OutgoingSegment HelpStrings =
        ("/小画家:启用:{number}（每人每日上限，1~5）\n" +
         "/小画家:禁用").ToMilkyTextSegment();

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return false;

        var command = message.ToTextCommands().FirstOrDefault();
        if (command is null || command.Name != "小画家") return false;
        if (command.Arguments.FirstOrDefault() is not ("启用" or "禁用")) return false;

        return await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, PermissionName,
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
            case ["启用", var rawNumber, ..]:
                await HandleEnableAsync(message, peerId, rawNumber, cancellationToken);
                return;
            case ["禁用", ..]:
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.Groups.Remove(peerId);
                    await config.SaveAsync(value, token);
                    await message.ReplyAsGroup(bot, token, ["已关闭本群绘图功能。".ToMilkyTextSegment()]);
                }, cancellationToken);
                return;
            default:
                await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
        }
    }

    private async ValueTask HandleEnableAsync(Event<IncomingMessage> message, long peerId, string rawNumber,
        CancellationToken cancellationToken)
    {
        var options = config.Current;
        if (!int.TryParse(rawNumber, out var number)
            || !PainterQuota.IsValidDailyLimit(number, options.MaxDailyLimit))
        {
            await message.ReplyAsGroup(bot, cancellationToken,
                [$"参数错误，请使用：/小画家:启用:{{number}}（1~{options.MaxDailyLimit}）"
                    .ToMilkyTextSegment()]);
            return;
        }

        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            value.Groups[peerId] = number;
            await config.SaveAsync(value, token);
            await message.ReplyAsGroup(bot, token,
                [$"已开启本群绘图功能，每人每天最多绘制 {number} 张。".ToMilkyTextSegment()]);
        }, cancellationToken);
    }
}
