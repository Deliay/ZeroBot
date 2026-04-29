using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.AI.Agents;
using ZeroBot.AI.Groups;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;

namespace ZeroBot.AI.Commands;

public class TrpgCommandHandler(
    IBotContext bot, 
    ICommandDispatcher dispatcher, 
    GroupAgentManager agentManager, 
    ILogger<TrpgCommandHandler> logger)
    : CommandQueuedHandler(dispatcher)
{
    private static readonly OutgoingSegment[] SceneHelpSegments =
        ["/剧本:设置:[剧本内容,不填则AI生成]\n/剧本:开始 - 开始跑团\n/剧本:清空 - 清空所有上下文".ToMilkyTextSegment()];
    private static readonly OutgoingSegment[] CharaHelpSegments =
        ["/角色卡:随机:[名字]\n/角色卡:改名:[名字]".ToMilkyTextSegment()];
    private static readonly OutgoingSegment[] GameHelpSegments =
        ["/跑团:[行动]".ToMilkyTextSegment()];

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();

        return ValueTask.FromResult(text.StartsWith("/剧本") || text.StartsWith("/角色卡") || text.StartsWith("/跑团"));
    }

    private ValueTask ClearAgent(long groupId, CancellationToken cancellationToken = default)
    {
        var agent = agentManager.GetAgent<TrpgAgentFactory>(groupId);
        return agent.ClearSessionAsync(cancellationToken);
    }

    private ValueTask HandleSceneCommand(Event<IncomingMessage> message, ITextCommand cmd,
        CancellationToken cancellationToken = default)
    {
        var agent = agentManager.GetAgent<TrpgAgentFactory>(message.Data.PeerId);
        var subCmd = cmd.Arguments.Length > 0 ? cmd.Arguments[0] : string.Empty;
        return subCmd switch
        {
            "设置" or "开始" => agent.EnqueueMessage(message, cancellationToken),
            "清空" => ClearAgent(message.Data.PeerId, cancellationToken),
            _ => message.Reply(bot, cancellationToken, SceneHelpSegments),
        };
    }

    private ValueTask HandleCharacter(Event<IncomingMessage> message, ITextCommand cmd,
        CancellationToken cancellationToken = default)
    {
        var agent = agentManager.GetAgent<TrpgAgentFactory>(message.Data.PeerId);
        var subCmd = cmd.Arguments.Length > 0 ? cmd.Arguments[0] : string.Empty;
        return subCmd switch
        {
            "随机" or "改名" => agent.EnqueueMessage(message, cancellationToken),
            _ => message.Reply(bot, cancellationToken, CharaHelpSegments),
        };
    }
    
    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await DequeueAsyncCore(@event, cancellationToken);
        }
        catch (Exception e)
        {
            await @event.Reply(bot, cancellationToken, [$"命令执行出错: {e.Message}".ToMilkyTextSegment()]);
            logger.LogError(e, "An error occured during executing command!");
        }
    }

    private async ValueTask DequeueAsyncCore(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var agent = agentManager.GetAgent<TrpgAgentFactory>(@event.Data.PeerId);
        foreach (var cmd in @event.ToTextCommands())
        {
            switch (cmd.Name)
            {
                case "剧本":
                    await HandleSceneCommand(@event, cmd, cancellationToken);
                    break;
                case "角色卡":
                    await HandleCharacter(@event, cmd, cancellationToken);
                    break;
                case "跑团":
                    await agent.EnqueueMessage(@event, cancellationToken);
                    break;
            }
        }
    }
}