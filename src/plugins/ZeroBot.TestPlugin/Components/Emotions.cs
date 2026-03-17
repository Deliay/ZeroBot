using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.TestPlugin.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.TestPlugin.Components;

public class Emotions(
    IBotContext bot,
    ICommandDispatcher dispatcher,
    ILogger<Emotions> logger,
    IJsonConfig<EmotionConfig> config) : CommandQueuedHandler(dispatcher)
{
    private static string GetRandomImagePathFromDir(string dir)
    {
        return Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Shuffle()
            .First();
    }
    
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText();
        var cmd = text[1..];
        return ValueTask.FromResult(config.Current.Commands.ContainsKey(cmd));
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var text = @event.ToText();
        var cmd = text[1..];
        if (!config.Current.Commands.TryGetValue(cmd, out var dir)) throw new InvalidOperationException("未知的表情包");
        
        var realPath = Path.Combine(config.Current.BaseDir, dir);
        try
        {
            await @event.Send(bot, cancellationToken, [
                await GetRandomImagePathFromDir(realPath)
                    .ToMilkyNonLocalImageSegmentAsync(cancellationToken),
            ]);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while sending message");
        }
    }
}