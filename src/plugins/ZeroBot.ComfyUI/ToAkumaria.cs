using ComfySharpSDK;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;

namespace ZeroBot.ComfyUI;

public class ToAkumaria(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    ComfyClient comfy,
    ILogger<ToAkumaria> logger)
    : CommandQueuedHandler(dispatcher)
{
    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var image = await @event.GetMilkyImageMessagesAsync(bot, cancellationToken)
                .FirstOrDefaultAsync(cancellationToken);
            if (image is null) return;
            var imageData = await image.GetMilkyImageBytesAsync(bot, @event, cancellationToken);
            var workflow = Workflow.LoadFromFile("./workflows/to-akumaria-api.json");
            workflow.SetNodeValue("74", "data", Convert.ToBase64String(imageData));
            var result = await comfy.RunWorkflowAndGetImagesAsync(workflow, cancellationToken: cancellationToken);
            await @event.ReplyAsGroup(bot, cancellationToken, result
                .Select(img => img.Data)
                .Where(data => data is { Length: > 0 })
                .Select(OutgoingSegment (data) => data!.ToMilkyImageSegment())
                .ToArray());
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

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        if (message.Data.ToText().Trim() != "/变毬") return false;
        if (!await message.GetMilkyImageMessagesAsync(bot, cancellationToken).AnyAsync(cancellationToken)) return false;
        
        return await permission.CheckGroupPermissionAsync(message.Data.PeerId, "comfyUi.to_akumaria",
            cancellationToken);
    }

    protected override async ValueTask EnqueueInspectorAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        await @event.AddReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
    }
}