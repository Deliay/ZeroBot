using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;
using ZeroBot.Utility.Commands;

namespace ZeroBot.Endfield.Component;

public class HypergraphyCommand(ICommandDispatcher dispatcher, IBotContext bot) : CommandQueuedHandler(dispatcher)
{
    private const string HelpStrings = "鹰角小助手\n" +
                                       "===私聊指令===" +
                                       "/鹰角:绑定\n" +
                                       "/鹰角:已绑\n" +
                                       "/鹰角:解绑:id\n" +
                                       "/鹰角:开启森空岛自动签到\n" +
                                       "/鹰角:关闭森空岛自动签到\n" +
                                       "===全局指令===" +
                                       "以后会有我的信息，我的体力一类的，我的抽卡记录等信息，群聊可用，但现在暂时还没有";
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.ToText().Trim().StartsWith("/鹰角"));
    }

    private ValueTask BindingAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private ValueTask MyBindingsAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private ValueTask UnboundAsync(int id, Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private ValueTask EnabledDailySignAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private ValueTask DisableDailySignAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private ValueTask HelpAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return message.SendAsPrivate(bot, cancellationToken, HelpStrings.ToMilkyTextSegment());
    }
    
    protected override ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var cmd = @event.ToTextCommands().First();
        return cmd.ParseNextArgument<string>() switch
        {
            "绑定" => BindingAsync(@event, cancellationToken),
            "已绑" => MyBindingsAsync(@event, cancellationToken),
            "解绑" when (cmd.Arguments.Length == 2) => UnboundAsync(cmd.ParseNextArgument<int>(), @event,
                cancellationToken),
            "开启森空岛自动签到" => EnabledDailySignAsync(@event, cancellationToken),
            "关闭森空岛自动签到" => DisableDailySignAsync(@event, cancellationToken),
            _ => HelpAsync(@event, cancellationToken)
        };
    }
}