namespace ZeroBot.AI.Agents;

public class AgentManager(IEnumerable<IAgentFactory> agentFactories)
{
    private Dictionary<string, IAgentFactory> _factories = agentFactories
        .ToDictionary(f => f.Id, f => f);
    
    public IEnumerable<IAgentFactory> Agents => _factories.Values;
    
    public IAgentFactory this[string id] => _factories[id];
    
    public IAgentFactory GetFactory(string id) => this[id];
    
    public IAgentFactory GetFactory<T>() where T : IAgentFactory, IAgentInfo => this[T.Id];
}
