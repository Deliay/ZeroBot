using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin.Components;

public partial class FakeNapCat(
    IBotContext bot,
    ILogger<FakeNapCat> logger): MessageQueueHandler<FakeNapCat>(bot, logger)
{
    private readonly IBotContext _bot = bot;

    [GeneratedRegex("^#([a-zA-Z0-9]+)cat$")]
    private static partial Regex NapCatCommandRegexGenerator();
    private static readonly Regex NapCatCommandRegex = NapCatCommandRegexGenerator();

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        var text = @event.ToText();
        if (!NapCatCommandRegex.IsMatch(text)) return;
        var platform = NapCatCommandRegex.Match(text).Groups[1].Value;
        var head = platform[0];
        var tail = platform[1..];
        await @event.Send(_bot, cancellationToken, [
            $"{head}{tail}Cat 信息\n版本: 11.45.14\n平台: linux (64-bit)\n运行时间: 1919天 8小时 10分钟".ToMilkyTextSegment()
        ]);
    }
}