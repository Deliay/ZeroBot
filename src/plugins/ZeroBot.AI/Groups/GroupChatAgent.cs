using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.AI.Agents;
using ZeroBot.Utility;

namespace ZeroBot.AI.Groups;

public class GroupChatAgent(
    IBotContext bot,
    ILogger<GroupChatAgent> logger,
    AgentSessionManager sessionManager)
{
    private string _sessionId = null!;
    private AgentSession? Session { get; set; }
    private Dictionary<long, Channel<Event<IncomingMessage>>> EnqueuedMessages { get; } = new();

    private AIAgent Agent { get; set; }

    public ValueTask ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        return sessionManager.ClearSessionAsync(_sessionId, cancellationToken);
    }
    
    private async ValueTask<ChatMessage> Convert(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        List<AIContent> contents =
        [
            new TextContent(JsonSerializer.Serialize(new
            {
                userId = message.Data.SenderId,
                groupId = message.Data.PeerId,
                text = message.Data.ToAgentText(),
            }))
        ];
        foreach (var img in message.Data.Segments.OfType<ImageIncomingSegment>())
        {
            contents.Add(new UriContent(await img.GetMilkyImageUrlAsync(bot, message, cancellationToken), "image/*"));
        }

        return new ChatMessage(ChatRole.User, contents);
    }
    
    private async Task AgentLoop(ChannelReader<Event<IncomingMessage>> queue, CancellationToken cancellationToken = default)
    {
        if (Session is null) throw new InvalidOperationException("Agent session not initialized");
        await foreach (var incomingMessage in queue.ReadAllAsync(cancellationToken))
        {
            logger.LogInformation("AI Agent incoming message: {}", incomingMessage.ToText());
            var msg = await Convert(incomingMessage, cancellationToken);
            var res = await Agent.RunAsync(msg, Session, cancellationToken: cancellationToken);
            logger.LogInformation("AI Agent run result {}", res.Text);
            var data = await Agent.SerializeSessionAsync(Session, cancellationToken: cancellationToken);
            await sessionManager.SaveSessionAsync(_sessionId, data, cancellationToken);
        }
    }
    
    public async ValueTask Initialize(long groupId, AIAgent agent, CancellationToken cancellationToken = default)
    {
        if (Session is not null) return;

        Agent = agent;
        
        _sessionId = $"agent-{Agent.Id}-group-{groupId}";

        var session = await sessionManager.GetSessionAsync(_sessionId, cancellationToken);
        if (session.HasValue)
        {
            Session = await Agent.DeserializeSessionAsync(session.Value, cancellationToken: cancellationToken);
        }
        else
        {
            Session = await Agent.CreateSessionAsync(cancellationToken);
        }

        if (!EnqueuedMessages.TryGetValue(groupId, out var channel))
        {
            EnqueuedMessages.Add(groupId, channel = Channel.CreateUnbounded<Event<IncomingMessage>>());
        }
        _ = AgentLoop(channel, cancellationToken);
    }
    
    public async ValueTask EnqueueMessage(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        await EnqueuedMessages[message.Data.PeerId].Writer.WriteAsync(message, cancellationToken);
    }
}