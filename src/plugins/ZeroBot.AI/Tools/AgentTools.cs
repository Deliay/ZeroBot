using System.Collections;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.AI.Tools;

public class AgentTools(IServiceProvider sp)
{
    public IEnumerable<AITool> GetTools()
    {
        return sp.GetServices<IToolProvider>()
            .SelectMany(t => t.GetTools());
    }
}
