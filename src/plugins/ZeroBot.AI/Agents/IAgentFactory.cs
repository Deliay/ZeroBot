using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ZeroBot.AI.Agents;

public interface IAgentInfo
{
    public static abstract string Id { get; }
    public static abstract string Name { get; }
    public static abstract string Description { get; }
}

public interface IAgentFactory
{
    public new string Id { get; }
    public new string Name { get; }
    public new string Description { get; }
    public AIAgent Create(IChatClient underlyingClient);
}
