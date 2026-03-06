using System.Buffers;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Endfield.Config;
using ZeroBot.Endfield.Extension;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;

namespace ZeroBot.Endfield.Component;

public class HypergraphyCommand(
    ICommandDispatcher dispatcher,
    BindingCommandHandlers binding,
    EndfieldCommandHandlers endfield,
    IBotContext bot) : CommandQueuedHandler(dispatcher)
{
    private static readonly TextOutgoingSegment HelpStrings = ("鹰角小助手\n" +
                                       "===私聊指令===\n" +
                                       "/鹰角:绑定 (添加时会自动进行签到)\n" +
                                       "/鹰角:已绑\n" +
                                       "/鹰角:解绑:本地ID\n" +
                                       "/鹰角:自动签到:本地ID \n" +
                                       "/鹰角:关闭自动签到:本地ID\n" +
                                       "\n===通用指令===\n" +
                                       "/zmd:我的信息").ToMilkyTextSegment();

    private ValueTask HelpAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return message.Reply(bot, cancellationToken, [HelpStrings]);
    }

    private ValueTask BindingCommandDispatchAsync(Event<IncomingMessage> @event,
        ITextCommand cmd,
        CancellationToken cancellationToken = default)
    {
        if (cmd.Arguments.Length == 0) return HelpAsync(@event, cancellationToken);
        return cmd.Arguments[0] switch
        {
            "绑定" => binding.BindingAsync(@event, cancellationToken),
            "已绑" => binding.MyBindingsAsync(@event, cancellationToken),
            "解绑" when (cmd.Arguments.Length == 2) => binding.UnboundAsync(cmd.Arguments[1], @event,
                cancellationToken),
            "自动签到" when (cmd.Arguments.Length == 2) => binding.EnabledDailySignAsync(cmd.Arguments[1], @event, cancellationToken),
            "关闭自动签到" when (cmd.Arguments.Length == 2) => binding.DisableDailySignAsync(cmd.Arguments[1], @event, cancellationToken),
            _ => HelpAsync(@event, cancellationToken)
        };
    }

    private ValueTask EndfieldCommandDispatchAsync(
        Event<IncomingMessage> @event,
        ITextCommand cmd,
        CancellationToken cancellationToken = default)
    {
        if (cmd.Arguments.Length == 0) return endfield.MyEndfieldInfoAsync(@event, cancellationToken);
        return cmd.Arguments[0] switch
        {
            "我的信息" => endfield.MyEndfieldInfoAsync(@event, cancellationToken),
            _ => HelpAsync(@event, cancellationToken)
        };
    }

    private ValueTask DequeueCoreAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var cmd = @event.ToTextCommands(argumentSplitters: ":：").First();

        return cmd.Name switch
        {
            "鹰角" => BindingCommandDispatchAsync(@event, cmd, cancellationToken),
            "zmd" => EndfieldCommandDispatchAsync(@event, cmd, cancellationToken),
            _ => ValueTask.CompletedTask
        };
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await DequeueCoreAsync(@event, cancellationToken);
        }
        catch (Exception e)
        {
            await @event.Reply(bot, cancellationToken, [e.Message.ToMilkyTextSegment()]);
        }
    }
    
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var msg = message.ToText().Trim();
        return ValueTask.FromResult(msg.StartsWith("/鹰角") || msg.StartsWith("/zmd"));
    }
}
