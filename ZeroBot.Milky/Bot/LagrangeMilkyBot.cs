using System.Threading.Channels;
using Milky.Net.Client;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Milky.Bot;

public class LagrangeMilkyBot(
    MilkyClient milky,
    BotInfos botInfos,
    MilkyWebSocketReceiver receiver,
    IBotContext botContext) : IBotService, IAsyncDisposable
{
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await botContext.RegisterBotAsync(this, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var @event in receiver.ReadEvents(cancellationToken))
            {
                await botContext.WriteEvent(@event, cancellationToken);
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
