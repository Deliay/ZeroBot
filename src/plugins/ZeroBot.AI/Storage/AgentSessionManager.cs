using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using OpenAI.Chat;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ZeroBot.AI.Storage;

public class AgentSessionManager(
    IBotContext bot,
    IJsonConfig<AgentConfig> config, 
    AIAgent agent,
    ILogger<AgentSessionManager> logger)
{
    private Dictionary<long, AgentSession> Sessions { get; } = new();
    private Dictionary<long, Channel<Event<IncomingMessage>>> EnqueuedMessages { get; } = new();

    public void CleanSession(long groupId)
    {
        Sessions.Remove(groupId);
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
    
    private async Task AgentLoop(ChannelReader<Event<IncomingMessage>> queue, AgentSession session, CancellationToken cancellationToken = default)
    {
        await foreach (var incomingMessage in queue.ReadAllAsync(cancellationToken))
        {
            logger.LogInformation("AI Agent incoming message: {}", incomingMessage.ToText());
            var msg = await Convert(incomingMessage, cancellationToken);
            var res = await agent.RunAsync(msg, session, cancellationToken: cancellationToken);
            logger.LogInformation("AI Agent run result {}", res.Text);
            var data = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            await SaveSessionAsync(incomingMessage.Data.PeerId, data, cancellationToken);
        }
    }
    
    private async ValueTask Initialize(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        var groupId = message.Data.PeerId;
        if (Sessions.ContainsKey(groupId)) return;

        if (config.Current.Storage.TryGetValue(groupId, out var sessionJson))
        {
            var session = await agent.DeserializeSessionAsync(sessionJson, cancellationToken: cancellationToken);
            Sessions.Add(groupId, session);
        }
        else
        {
            Sessions.Add(groupId, await agent.CreateSessionAsync(cancellationToken));
        }

        if (!EnqueuedMessages.TryGetValue(groupId, out var channel))
        {
            EnqueuedMessages.Add(groupId, channel = Channel.CreateUnbounded<Event<IncomingMessage>>());
        }
        _ = AgentLoop(channel, Sessions[groupId], cancellationToken);
    }
    
    public async ValueTask EnqueueMessage(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        await Initialize(message, cancellationToken);
        await EnqueuedMessages[message.Data.PeerId].Writer.WriteAsync(message, cancellationToken);
    }
    
    private async ValueTask SaveSessionAsync(long groupId, JsonElement session, CancellationToken cancellationToken = default)
    {
        await config.BeginConfigMutationScopeAsync(async (cfg, ct) =>
        {
            cfg.Storage[groupId] = session;
            await config.SaveAsync(cfg, ct);
        }, cancellationToken);
    } 
}