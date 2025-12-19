using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;

namespace ZeroBot.PermissionCommandPlugin;

public class PermissionManagerCommand(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    ILogger<PermissionManagerCommand> logger)
    : CommandHandler(dispatcher)
{

    private static MessageScene Parse(string str)
    {
        return str switch
        {
            "u" => MessageScene.Friend,
            "g" => MessageScene.Group,
            _ => throw new ArgumentOutOfRangeException(nameof(str)),
        };
    }
    
    static PermissionManagerCommand()
    {
        CommandArgParsers.RegisterParser(Parse);
    }
    
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.Data is GroupIncomingMessage && message.Data.ToText().Trim().StartsWith("/$权限设置"));
    }

    private async ValueTask<bool> IsSudoerAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return await permission.CheckUserPermissionAsync(message.Data.SenderId, "sudoers",
            cancellationToken);
    }
    
    private static readonly TextOutgoingSegment HelpStrings = 
        ("/$权限设置:g:enable:{perm}\n" +
        "/$权限设置:g:disable:{perm}\n").ToSegment();

    private async ValueTask<bool> VerifyGroupPermissionAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (await IsSudoerAsync(message, cancellationToken)) return true;
        var groupMembers = await bot.GetGroupMembersAsync(message.SelfId, message.Data.PeerId, cancellationToken);
        return groupMembers is not null &&
               groupMembers.Members.Any(m => m.UserId == message.Data.SenderId && m.Role != Role.Member);
    }
    
    private Func<MessageScene, string, string, CancellationToken, Task> ManageGroupPermission(Event<IncomingMessage> message)
    {
        return async (scene, op, perm, cancellationToken) =>
        {
            if (!await VerifyGroupPermissionAsync(message, cancellationToken)) return;
            switch (op)
            {
                case "enable":
                    await permission.GrantGroupPermissionAsync(message.Data.PeerId, perm, cancellationToken);
                    await message.ReplyAsGroup(bot, cancellationToken, $"群已开启功能:{perm}".ToSegment());
                    break;
                case "disable":
                    await permission.RevokeGroupPermissionAsync(message.Data.PeerId, perm, cancellationToken);
                    await message.ReplyAsGroup(bot, cancellationToken, $"群已关闭功能:{perm}".ToSegment());
                    break;
                default:
                    await message.ReplyAsGroup(bot, cancellationToken, "未知的操作，只允许enable/disable".ToSegment());
                    break;
            }
        };
    }
    
    protected override async ValueTask HandleAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var command = message.Data.ToTextCommands().First();
        if (command.Arguments.Length == 0)
        { 
            await message.ReplyAsGroup(bot, cancellationToken, HelpStrings);
            return;            
        }

        if (command.Arguments[0] == "g")
        {
            try
            {
                await command.InvokeCommandAsync(ManageGroupPermission(message), cancellationToken);
            }
            catch (Exception e)
            {
                await message.ReplyAsGroup(bot, cancellationToken, e.Message.ToSegment());
                logger.LogError(e, "An error occurred while processing the command");
            }
            
        }
    }
}
