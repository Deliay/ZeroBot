using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin.Components;

record WishPayload(string wish);

record WishResult(string category, string? reason,  string? scenario);

record WishResponse(WishResult result);


public class Wish(ICommandDispatcher dispatcher, IBotContext bot, ILogger<Wish> logger) : CommandQueuedHandler(dispatcher)
{
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var text = message.ToText().Trim();
        return ValueTask.FromResult(text.StartsWith("/许愿"));
    }

    private const string WishUrl = "https://wish.closeai.moe/api/validateWish";
    private readonly HttpClient _client = new()
    {
        DefaultRequestHeaders =
        {
            { "origin",  "https://wish.closeai.moe" },
        },
        Timeout = TimeSpan.FromSeconds(15),
    };
    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var wish = @event.ToText().Trim()[3..];
        await @event.AddReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(WishUrl, new WishPayload(wish), cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<WishResponse>(cancellationToken);
            if (!response.IsSuccessStatusCode || result is null)
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    [$"请求失败，请稍后再重试喵~ HttpStatus: {response.StatusCode}".ToMilkyTextSegment()]);
                return;
            }

            switch (result.result.category)
            {
                case "allow":
                    await @event.ReplyAsGroup(bot, cancellationToken, [
                        $"你的愿望「{wish}」许愿成功，神已经应允了你的愿望：\n{result.result.scenario!}".ToMilkyTextSegment()
                    ]);
                    break;
                case "block":
                    await @event.ReplyAsGroup(bot, cancellationToken, [
                        $"你的愿望「{wish}」许愿失败，因为\n{result.result.reason!}".ToMilkyTextSegment()
                    ]);
                    break;
                default:
                    await @event.ReplyAsGroup(bot, cancellationToken, [
                        $"未知的许愿结果，找BOT的管理员看看为啥".ToMilkyTextSegment()
                    ]);
                    logger.LogWarning("Unknown case: {JSON}", JsonSerializer.Serialize(result));
                    break;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while processing the command");
        }
        finally
        {
            await @event.RemoveReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
        }
    }
}