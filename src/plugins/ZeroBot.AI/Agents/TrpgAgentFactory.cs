using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.AI.Skills;
using ZeroBot.AI.Tools;

namespace ZeroBot.AI.Agents;

public class TrpgAgentFactory(IServiceProvider sp) : IAgentFactory, IAgentInfo
{
    public const string AgentId = "zero-trpg-agent";
    public const string AgentName = "Zero Trpg Agent";
    public const string AgentDescription = "在QQ群内活跃的跑团(TRPG)主持人（KP）";
    
    [Experimental("MAAI001")]
    private ChatClientAgentOptions GetAgentOptions()
    {
        return new ChatClientAgentOptions()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            ChatOptions = new ChatOptions()
            {
                Tools = [
                    // QQ聊天相关tools
                    ..sp.GetRequiredService<ChatTools>().GetTools()
                ],
                Instructions = "你是在QQ群内活跃的跑团(TRPG)主持人（KP）"
            },
            AIContextProviders =
            [
                new AgentSkillsProviderBuilder()
                    // 跑团skills
                    .UseSkills(sp.GetRequiredService<TrpgSkill>().GetSkills())
                    .Build()
            ]
        };
    }

    static string IAgentInfo.Id => AgentId;

    static string IAgentInfo.Name => AgentName;

    static string IAgentInfo.Description => AgentDescription;

    public string Id => AgentId;
    public string Name => AgentName;
    public string Description => AgentDescription;

    [Experimental("MAAI001")]
    public AIAgent Create(IChatClient underlyingClient)
    {
        return new ChatClientAgent(underlyingClient, options: GetAgentOptions(), services: sp);
    }

}