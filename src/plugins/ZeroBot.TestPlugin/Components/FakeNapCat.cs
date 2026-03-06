using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin.Components;

public class FakeNapCat(
    IBotContext bot,
    ICommandDispatcher dispatcher) : CommandQueuedHandler(dispatcher)
{
    private const string NapCatCommand = "#napcat";

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message.ToText() == NapCatCommand);

    private static readonly OutgoingSegment[] NapCatInfoStrings =
    ["NapCat 信息\n版本: 11.45.14\n平台: linux (64-bit)\n运行时间: 1919天 8小时 10分钟".ToMilkyTextSegment()];

    protected override ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
        => @event.Send(bot, cancellationToken, NapCatInfoStrings);
}