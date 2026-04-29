using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.AI.Agents;
using ZeroBot.AI.Chats;

namespace ZeroBot.AI.Groups;

public class GroupAgentManager(IServiceProvider sp, AgentManager agentManager, ChatProviderManager providerManager)
{
    private readonly Dictionary<long, Dictionary<string, GroupChatAgent>> _agents = new();

    private async ValueTask<GroupChatAgent> CreateAgent(long groupId, string agentId, IChatClient client,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(groupId, out var groupAgents))
        {
            _agents.Add(groupId, groupAgents = new Dictionary<string, GroupChatAgent>());
        }

        if (groupAgents.ContainsKey(agentId))
        {
            throw new ArgumentException("Agent已经存在，重建需要手动移除后再添加");
        }
        var agent = agentManager.GetFactory(agentId).Create(client);
        var groupAgent = sp.GetRequiredService<GroupChatAgent>();
        await groupAgent.Initialize(groupId, agent, cancellationToken);
        groupAgents[agentId] = groupAgent;

        return groupAgent;
    }

    public void RemoveAgent(long groupId, string agentId)
    {
        if (!_agents.TryGetValue(groupId, out var groupAgents)) return;
        groupAgents.Remove(agentId);
    }
    
    public GroupChatAgent GetAgent<T>(long groupId) where T : IAgentFactory, IAgentInfo
    {
        if (!_agents.TryGetValue(groupId, out var groupAgents))
        {
            _agents.Add(groupId, groupAgents = new Dictionary<string, GroupChatAgent>());
        }

        var agentId = T.Id;
        return groupAgents.TryGetValue(agentId, out var groupAgent)
            ? groupAgent
            : throw new ArgumentException("Agent尚未添加，请先添加再进行操作");
    }
}