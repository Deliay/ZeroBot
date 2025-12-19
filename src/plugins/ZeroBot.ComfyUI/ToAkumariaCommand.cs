using System.Text;
using System.Threading.Channels;
using ComfySharpSDK;
using ComfySharpSDK.Domains;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;

namespace ZeroBot.ComfyUI;

public class ToAkumariaCommand(
    ICommandDispatcher dispatcher,
    IPermission permission,
    IBotContext bot,
    ComfyClient comfy,
    ILogger<ToAkumariaCommand> logger)
    : CommandHandler(dispatcher)
{
    private readonly Channel<Event<IncomingMessage>> _processQueue = Channel.CreateUnbounded<Event<IncomingMessage>>();
    private readonly HttpClient _httpClient = new();
    
    protected override ValueTask InitializeCommandAsync(CancellationToken cancellationToken = default)
    {
        _ = ProcessQueueAsync(cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var @event in _processQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var image = await @event.GetImageMessagesAsync(bot, cancellationToken)
                    .FirstOrDefaultAsync(cancellationToken);
                if (image is null) continue;
                var imageData = await _httpClient.GetByteArrayAsync(image.Data.TempUrl, cancellationToken);
                var workflow = Workflow.LoadFromFile("./workflows/to-akumaria-api.json");
                workflow.SetNodeValue("74", "data", Convert.ToBase64String(imageData));
                var result = await comfy.RunWorkflowAndGetImagesAsync(workflow, cancellationToken: cancellationToken);
                await bot.WriteManyGroupMessageAsync(@event.SelfId, [@event.Data.PeerId], cancellationToken,
                    result
                        .Select(img => img.Data)
                        .Where(data => data is { Length: > 0 })
                        .Select(OutgoingSegment (data) => data!.ToImageSegment())
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
    }
    
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        if (message.Data.ToText().Trim() != "/变毬") return false;
        if (!await message.GetImageMessagesAsync(bot, cancellationToken).AnyAsync(cancellationToken)) return false;
        
        return await permission.CheckGroupPermissionAsync(message.Data.PeerId, "comfyUi.to_akumaria",
            cancellationToken);
    }
    
    protected override async ValueTask HandleAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        await message.AddReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
        await _processQueue.Writer.WriteAsync(message, cancellationToken);
    }
}