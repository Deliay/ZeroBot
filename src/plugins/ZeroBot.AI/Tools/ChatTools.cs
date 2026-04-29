using System.Collections;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.AI.Tools;

public class ChatTools(IBotContext bot) : IToolProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    
    [Description("call when need to get group chat history")]
    public async ValueTask<string> GetGroupChatHistoryAsync(
        [Description("the groupId")] long groupId, 
        CancellationToken cancellationToken = default)
    {
        var account = await bot.GetAccountInfoAsync(cancellationToken).FirstAsync(cancellationToken);
        var msgList = await bot.EventRepository!.SearchEventAsync<IncomingMessage>(account.Uin)
            .Where(g => g.Data.PeerId == groupId)
            .OrderByDescending(g => g.Data.MessageSeq)
            .Take(50)
            .ToAsyncEnumerable()
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(msgList.Select(e => new
        {
            sender = e.Data.SenderId,
            seq = e.Data.MessageSeq,
            msg = e.Data.ToAgentText()
        }), SerializerOptions);
    }

    [Description("call when need to send message to group")]
    public async ValueTask SendMessage(
        [Description("the groupId")] long groupId, 
        [Description("the message you want to send")] string message, 
        CancellationToken cancellationToken = default)
    {
        var account = await bot.GetAccountInfoAsync(cancellationToken).FirstAsync(cancellationToken);
        await bot.WriteManyGroupMessageAsync(account.Uin, [groupId], cancellationToken, [message.ToMilkyTextSegment()]);
    }

    public async ValueTask ReplyMessage(
        [Description("the groupId")] long groupId, 
        [Description("the seq of the message you want to reply")] long replyMessageSeq, 
        [Description("the message you want to send")] string message,
        CancellationToken cancellationToken = default)
    {
        var account = await bot.GetAccountInfoAsync(cancellationToken).FirstAsync(cancellationToken);
        await bot.WriteManyGroupMessageAsync(account.Uin, [groupId], cancellationToken, message.ToMilkyTextSegment());
    }

    public IEnumerable<AITool> GetTools()
    {
        yield return AIFunctionFactory.Create(GetGroupChatHistoryAsync);
        yield return AIFunctionFactory.Create(SendMessage);
        yield return AIFunctionFactory.Create(ReplyMessage);
    }
}