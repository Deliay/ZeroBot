using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Workflow;

/// <summary>
/// 工作流功能群开关：/workflow:enable 与 /workflow:disable。
/// 仅群聊可用，且仅 bot 管理员（sudoers）可启用/禁用；群管理员无权操作。
/// 启用时写入群开关配置，禁用时移除该群配置。
/// </summary>
public class WorkflowManageCommandHandler(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    IJsonConfig<WorkflowGroupOptions> config) : CommandHandler(dispatcher)
{
    private static readonly OutgoingSegment HelpStrings =
        ("/workflow:enable\n" +
         "/workflow:disable").ToMilkyTextSegment();

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return false;

        var command = message.ToTextCommands().FirstOrDefault();
        if (command is null || !command.Name.Equals("workflow", StringComparison.OrdinalIgnoreCase)) return false;
        if (command.Arguments.FirstOrDefault() is not ("enable" or "disable")) return false;

        // 仅 bot 管理员（sudoers）可启用/禁用，群管理员无权操作。
        return await permission.IsSudoerAsync(message.Data.SenderId, cancellationToken);
    }

    protected override async ValueTask HandleAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var command = message.ToTextCommands().FirstOrDefault();
        var action = command?.Arguments.FirstOrDefault();
        var peerId = message.Data.PeerId;

        switch (action)
        {
            case "enable":
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.EnabledGroups.Add(peerId);
                    await config.SaveAsync(value, token);
                }, cancellationToken);

                await message.ReplyAsGroup(bot, cancellationToken,
                    ["已开启本群工作流功能。".ToMilkyTextSegment()]);
                return;

            case "disable":
                await config.BeginConfigMutationScopeAsync(async (value, token) =>
                {
                    value.EnabledGroups.Remove(peerId);
                    await config.SaveAsync(value, token);
                }, cancellationToken);

                await message.ReplyAsGroup(bot, cancellationToken,
                    ["已关闭本群工作流功能。".ToMilkyTextSegment()]);
                return;

            default:
                await message.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
        }
    }
}
