using System.Text.Json;
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
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    public async ValueTask<string> GetTempResourceUrlAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await milky.Message.GetResourceTempUrlAsync(new GetResourceTempUrlInput(id), cancellationToken);
        return result.Url;
    }

    public async ValueTask<GetLoginInfoOutput> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        return await botInfos.GetAccountInfoAsync(cancellationToken);
    }

    public async ValueTask<Event<IncomingMessage>> GetGroupMessageAsync(MessageScene scene, long peerId, long messageId,
        CancellationToken cancellationToken = default)
    {
        var result = await milky.Message.GetMessageAsync(new GetMessageInput(scene, peerId, messageId),
            cancellationToken);
        var account = await GetCurrentAccountAsync(cancellationToken);
        return new Event<IncomingMessage>(result.Message.Time, account.Uin, result.Message);
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

    public async ValueTask UpdateGroupReactionAsync(long groupId, long messageId, string reactionId, bool add,
        CancellationToken cancellationToken = default)
    {
        await milky.Group.SendGroupMessageReactionAsync(
            new SendGroupMessageReactionInput(groupId, messageId, reactionId, add), cancellationToken);
    }

    public async ValueTask<GetGroupInfoOutput> GetGroupInformationAsync(long groupId,
        CancellationToken cancellationToken = default)
    {
        return await botInfos.GetGroupInfoAsync(groupId, cancellationToken);
    }
    
    public async ValueTask<GetGroupMemberListOutput?> GetGroupMembersAsync(long groupId,
        CancellationToken cancellationToken = default)
    {
        return await botInfos.GetGroupMembersAsync(groupId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await botContext.UnregisterBot(this);
    }
}
