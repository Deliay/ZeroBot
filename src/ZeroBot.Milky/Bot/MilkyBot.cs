using Microsoft.Extensions.Logging;
using Milky.Net.Client;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Milky.Bot;

public class MilkyBot(
    MilkyClient milky,
    BotInfos botInfos,
    MilkyWebSocketReceiver receiver,
    IBotContext botContext,
    ILogger<MilkyBot> logger) : IBotService, IAsyncDisposable
{
    private async Task ReadEvents(CancellationToken cancellationToken = default)
    {
        await foreach (var @event in receiver.ReadEvents(cancellationToken))
        {
            logger.LogInformation("Received event: {@event}", @event.GetType());
            if (@event is Event<IncomingMessage> message)
            {
                logger.LogInformation($"({message.Data.PeerId}) {message.Data.SenderId}: {string.Join("", message.Data.Segments)}");
            }
            await botContext.WriteEvent(@event, cancellationToken);
        }
    }
    
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await botContext.RegisterBotAsync(this, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReadEvents(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while receiving events");
            }
        }
    }

    public async ValueTask<GetLoginInfoOutput> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        return await botInfos.GetAccountInfoAsync(cancellationToken);
    }

    public async ValueTask<MultiGroupSendResult> SendGroupMessageAsync(HashSet<long> groupIds, CancellationToken cancellationToken = default,
        params OutgoingSegment[] messageSegments)
    {
        var result = new MultiGroupSendResult();
        foreach (var groupId in groupIds)
        {
            try
            {
                var requestArgs = new SendGroupMessageInput(groupId, messageSegments);
                var sendResult = await milky.Message.SendGroupMessageAsync(requestArgs, cancellationToken);

                result.Add(groupId, sendResult);
            }
            catch
            {
                // do nothing
            }
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await botContext.UnregisterBot(this);
    }
}
