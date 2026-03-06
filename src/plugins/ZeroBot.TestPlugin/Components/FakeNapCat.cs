using System.Text.RegularExpressions;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin.Components;

public partial class FakeNapCat(
    IBotContext bot,
    ICommandDispatcher dispatcher) : CommandQueuedHandler(dispatcher)
{
    [GeneratedRegex("^#(.*)cat$")]
    private static partial Regex NapCatCommandRegexGenerator();
    private static readonly Regex NapCatCommandRegex = NapCatCommandRegexGenerator();

    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(NapCatCommandRegex.IsMatch(message.ToText()));

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        var platform = NapCatCommandRegex.Match(@event.ToText()).Groups[1].Value;
        var head = platform[0];
        var tail = platform[1..];
        await @event.Send(bot, cancellationToken, [
            $"{head}{tail}Cat 信息\n版本: 11.45.14\n平台: linux (64-bit)\n运行时间: 1919天 8小时 10分钟".ToMilkyTextSegment()
        ]);
    }
}