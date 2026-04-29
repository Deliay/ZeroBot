using Microsoft.Extensions.AI;

namespace ZeroBot.AI.Tools;

public interface IToolProvider
{
    public IEnumerable<AITool> GetTools();
}